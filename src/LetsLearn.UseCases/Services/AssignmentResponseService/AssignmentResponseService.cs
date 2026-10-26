using System.Text.Json;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Core.Shared;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;

namespace LetsLearn.UseCases.Services.AssignmentResponseService
{
    public class AssignmentResponseService : IAssignmentResponseService
    {
        private const decimal MaximumGrade = 10m;
        private const int MaximumTeacherFeedbackLength = 4000;

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILearningProgressService _learningProgressService;

        public AssignmentResponseService(IUnitOfWork unitOfWork, ILearningProgressService learningProgressService)
        {
            _unitOfWork = unitOfWork;
            _learningProgressService = learningProgressService;
        }

        public async Task ValidateStudentCanSubmitAsync(Guid topicId, Guid studentId, CancellationToken ct = default)
        {
            var courseId = await ResolveAssignmentCourseIdAsync(topicId, ct);
            await EnsureStudentEnrolledAsync(studentId, courseId, ct);
        }

        public async Task<AssignmentResponseDTO> GetAssigmentResponseByIdAsync(
            Guid id,
            Guid requesterId,
            string? requesterRole,
            Guid? routeTopicId = null,
            CancellationToken ct = default)
        {
            var entity = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(id)
                ?? throw new KeyNotFoundException("Assignment response not found.");

            if (routeTopicId.HasValue && entity.TopicId != routeTopicId.Value)
            {
                throw new KeyNotFoundException("Assignment response not found.");
            }

            await EnsureCanReadResponseAsync(entity, requesterId, requesterRole, ct);
            return ToDto(entity);
        }

        public async Task<AssignmentResponseDTO> CreateAssigmentResponseAsync(
            CreateAssignmentResponseRequest dto,
            Guid studentId,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            var now = DateTime.UtcNow;
            var entity = new AssignmentResponse
            {
                Id = Guid.NewGuid(),
                TopicId = dto.TopicId,
                StudentId = studentId,
                SubmittedAt = now,
                Note = dto.Note,
                Mark = null,
                Files = (dto.CloudinaryFiles ?? new List<CreateCloudinaryFileRequest>())
                    .Select(file => new CloudinaryFile
                    {
                        Id = Guid.NewGuid(),
                        Name = file.Name,
                        DisplayUrl = file.DisplayUrl,
                        DownloadUrl = file.DownloadUrl,
                        AssignmentResponseId = Guid.Empty
                    })
                    .ToList()
            };

            foreach (var file in entity.Files)
            {
                file.AssignmentResponseId = entity.Id;
            }

            await _unitOfWork.AssignmentResponses.AddAsync(entity);
            await _unitOfWork.CloudinaryFiles.AddRangeAsync(entity.Files);
            await _unitOfWork.LearningActivityLogs.AddAsync(new LearningActivityLog
            {
                Id = Guid.NewGuid(),
                UserId = studentId,
                CourseId = await ResolveCourseIdByTopicAsync(dto.TopicId, ct),
                TopicId = dto.TopicId,
                EventType = "assignment_submitted",
                EventSource = "assignment_response",
                Metadata = JsonSerializer.Serialize(new
                {
                    assignmentResponseId = entity.Id,
                    fileCount = entity.Files.Count,
                    submittedAt = entity.SubmittedAt
                }),
                OccurredAt = now
            });

            await _unitOfWork.CommitAsync();
            await _learningProgressService.MarkTopicCompletedAsync(dto.TopicId, studentId, "submit_assignment", ct);
            return ToDto(entity);
        }

        public async Task<IEnumerable<AssignmentResponseDTO>> GetAllAssigmentResponseByTopicIdAsync(
            Guid topicId,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            var courseId = await ResolveAssignmentCourseIdAsync(topicId, ct);
            await EnsureTeacherCanManageCourseAsync(requesterId, requesterRole, courseId, ct);
            var entities = await _unitOfWork.AssignmentResponses.GetAllByTopicIdWithFilesAsync(topicId);
            return entities.Select(ToDto).ToList();
        }

        public async Task<IEnumerable<AssignmentResponseDTO>> GetAssigmentResponsesByTopicIdAndStudentIdAsync(
            Guid topicId,
            Guid studentId,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            var courseId = await ResolveAssignmentCourseIdAsync(topicId, ct);

            if (IsRole(requesterRole, AppRoles.Admin))
            {
                // Admin may inspect any student's submission.
            }
            else if (IsRole(requesterRole, AppRoles.Teacher))
            {
                await EnsureTeacherCanManageCourseAsync(requesterId, requesterRole, courseId, ct);
            }
            else if (IsRole(requesterRole, AppRoles.Student))
            {
                if (studentId != requesterId)
                {
                    throw new UnauthorizedAccessException("Students can only view their own assignment responses.");
                }

                await EnsureStudentEnrolledAsync(requesterId, courseId, ct);
            }
            else
            {
                throw new UnauthorizedAccessException("Role is not allowed to view assignment responses.");
            }

            var entity = await _unitOfWork.AssignmentResponses.GetByTopicIdAndStudentIdWithFilesAsync(topicId, studentId);
            return entity == null ? Enumerable.Empty<AssignmentResponseDTO>() : new[] { ToDto(entity) };
        }

        public async Task<AssignmentResponseDTO> UpdateAssigmentResponseByIdAsync(
            Guid topicId,
            Guid id,
            UpdateAssignmentResponseRequest dto,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            if (!IsRole(requesterRole, AppRoles.Student))
            {
                throw new UnauthorizedAccessException("Only the submitting student can update an assignment response.");
            }

            ArgumentNullException.ThrowIfNull(dto);
            ArgumentNullException.ThrowIfNull(dto.Data);
            var entity = await _unitOfWork.AssignmentResponses.GetByIdTrackedWithFilesAsync(id)
                ?? throw new KeyNotFoundException("Assignment response not found.");

            if (entity.TopicId != topicId)
            {
                throw new KeyNotFoundException("Assignment response not found.");
            }
            if (entity.StudentId != requesterId)
            {
                throw new UnauthorizedAccessException("Students can only update their own assignment responses.");
            }
            if (entity.Mark.HasValue || entity.GradedAt.HasValue)
            {
                throw new InvalidOperationException("A graded assignment response cannot be edited.");
            }

            var courseId = await ResolveAssignmentCourseIdAsync(topicId, ct);
            await EnsureStudentEnrolledAsync(requesterId, courseId, ct);

            // Only student-controlled fields are accepted. IDs, mark, grader, feedback,
            // and submission time in the legacy request body are deliberately ignored.
            entity.Note = dto.Data.Note;
            entity.SubmittedAt = DateTime.UtcNow;

            var oldFiles = entity.Files.ToList();
            if (oldFiles.Count > 0)
            {
                await _unitOfWork.CloudinaryFiles.DeleteRangeAsync(oldFiles);
                entity.Files.Clear();
            }

            var newFiles = (dto.Data.Files ?? new List<CloudinaryFile>())
                .Select(file => new CloudinaryFile
                {
                    Id = Guid.NewGuid(),
                    Name = file.Name,
                    DisplayUrl = file.DisplayUrl,
                    DownloadUrl = file.DownloadUrl,
                    AssignmentResponseId = entity.Id,
                    TopicFileId = null,
                    TopicAssignmentId = null
                })
                .ToList();

            foreach (var file in newFiles)
            {
                entity.Files.Add(file);
            }

            if (newFiles.Count > 0)
            {
                await _unitOfWork.CloudinaryFiles.AddRangeAsync(newFiles);
            }

            await _unitOfWork.AiAssignmentGradingJobs.MarkStaleForAssignmentResponseAsync(entity.Id, DateTime.UtcNow, ct);
            await _unitOfWork.CommitAsync();
            return ToDto(entity);
        }

        public async Task<AssignmentResponseDTO> GradeAssignmentResponseAsync(
            Guid topicId,
            Guid id,
            GradeAssignmentResponseRequest request,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.Mark.HasValue || request.Mark.Value < 0m || request.Mark.Value > MaximumGrade)
            {
                throw new ArgumentException("Mark is required and must be between 0 and 10.", nameof(request.Mark));
            }
            if (request.TeacherFeedback?.Length > MaximumTeacherFeedbackLength)
            {
                throw new ArgumentException($"Teacher feedback cannot exceed {MaximumTeacherFeedbackLength} characters.", nameof(request.TeacherFeedback));
            }

            var entity = await _unitOfWork.AssignmentResponses.GetByIdTrackedWithFilesAsync(id)
                ?? throw new KeyNotFoundException("Assignment response not found.");
            if (entity.TopicId != topicId)
            {
                throw new KeyNotFoundException("Assignment response not found.");
            }

            var courseId = await ResolveAssignmentCourseIdAsync(topicId, ct);
            await EnsureTeacherCanManageCourseAsync(requesterId, requesterRole, courseId, ct);

            await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                entity.Mark = request.Mark.Value;
                entity.TeacherFeedback = request.TeacherFeedback?.Trim();
                entity.GradedAt = DateTime.UtcNow;
                entity.GradedBy = requesterId;
                await _unitOfWork.LearningActivityLogs.AddAsync(new LearningActivityLog
                {
                    Id = Guid.NewGuid(),
                    UserId = requesterId,
                    CourseId = courseId,
                    TopicId = topicId,
                    EventType = "assignment_graded",
                    EventSource = "teacher_grade",
                    Metadata = JsonSerializer.Serialize(new
                    {
                        assignmentResponseId = entity.Id,
                        mark = entity.Mark
                    }),
                    OccurredAt = entity.GradedAt.Value
                });
                return true;
            }, ct);
            return ToDto(entity);
        }

        public async Task DeleteAssigmentResponseAsync(
            Guid topicId,
            Guid id,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            if (!IsRole(requesterRole, AppRoles.Student))
            {
                throw new UnauthorizedAccessException("Only the submitting student can delete an assignment response.");
            }

            var entity = await _unitOfWork.AssignmentResponses.GetByIdTrackedWithFilesAsync(id)
                ?? throw new KeyNotFoundException("Assignment response not found.");
            if (entity.TopicId != topicId)
            {
                throw new KeyNotFoundException("Assignment response not found.");
            }
            if (entity.StudentId != requesterId)
            {
                throw new UnauthorizedAccessException("Students can only delete their own assignment responses.");
            }
            if (entity.Mark.HasValue || entity.GradedAt.HasValue)
            {
                throw new InvalidOperationException("A graded assignment response cannot be deleted.");
            }
            if (await _unitOfWork.AiAssignmentGradingJobs.HasJobsForAssignmentResponseAsync(id, ct))
            {
                throw new InvalidOperationException("An assignment response with AI grading history cannot be deleted.");
            }

            var courseId = await ResolveAssignmentCourseIdAsync(topicId, ct);
            await EnsureStudentEnrolledAsync(requesterId, courseId, ct);
            await _unitOfWork.AssignmentResponses.DeleteAsync(entity);
            await _unitOfWork.CommitAsync();
        }

        private async Task EnsureCanReadResponseAsync(
            AssignmentResponse response,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct)
        {
            var courseId = await ResolveAssignmentCourseIdAsync(response.TopicId, ct);
            if (IsRole(requesterRole, AppRoles.Admin)) return;
            if (IsRole(requesterRole, AppRoles.Teacher))
            {
                await EnsureTeacherCanManageCourseAsync(requesterId, requesterRole, courseId, ct);
                return;
            }
            if (IsRole(requesterRole, AppRoles.Student) && response.StudentId == requesterId)
            {
                await EnsureStudentEnrolledAsync(requesterId, courseId, ct);
                return;
            }

            throw new UnauthorizedAccessException("You are not allowed to view this assignment response.");
        }

        private async Task EnsureTeacherCanManageCourseAsync(
            Guid requesterId,
            string? requesterRole,
            string courseId,
            CancellationToken ct)
        {
            if (IsRole(requesterRole, AppRoles.Admin)) return;
            if (!IsRole(requesterRole, AppRoles.Teacher))
            {
                throw new UnauthorizedAccessException("Only the course teacher or an admin can manage assignment responses.");
            }

            var course = await _unitOfWork.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found.");
            if (course.CreatorId != requesterId)
            {
                throw new UnauthorizedAccessException("Teachers can only manage assignment responses in their own courses.");
            }
        }

        private async Task EnsureStudentEnrolledAsync(Guid studentId, string courseId, CancellationToken ct)
        {
            var enrollment = await _unitOfWork.Enrollments.GetByIdsAsync(studentId, courseId, ct);
            if (enrollment == null)
            {
                throw new UnauthorizedAccessException("Student is not enrolled in this course.");
            }
        }

        private async Task<string> ResolveAssignmentCourseIdAsync(Guid topicId, CancellationToken ct)
        {
            var topic = await _unitOfWork.Topics.GetByIdAsync(topicId, ct)
                ?? throw new KeyNotFoundException("Assignment topic not found.");
            if (!string.Equals(topic.Type, "assignment", StringComparison.OrdinalIgnoreCase))
            {
                throw new KeyNotFoundException("Assignment topic not found.");
            }

            var assignment = await _unitOfWork.TopicAssignments.GetByIdAsync(topicId, ct);
            if (assignment == null)
            {
                throw new KeyNotFoundException("Assignment not found.");
            }

            var section = await _unitOfWork.Sections.GetByIdAsync(topic.SectionId, ct)
                ?? throw new KeyNotFoundException("Section not found.");
            _ = await _unitOfWork.Course.GetByIdAsync(section.CourseId, ct)
                ?? throw new KeyNotFoundException("Course not found.");
            return section.CourseId;
        }

        private async Task<string?> ResolveCourseIdByTopicAsync(Guid topicId, CancellationToken ct)
        {
            var topic = await _unitOfWork.Topics.GetByIdAsync(topicId, ct);
            if (topic == null) return null;
            var section = await _unitOfWork.Sections.GetByIdAsync(topic.SectionId, ct);
            return section?.CourseId;
        }

        private static bool IsRole(string? role, string expected) =>
            expected == AppRoles.Student
                ? AppRoles.IsStudent(role)
                : string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);

        private static AssignmentResponseDTO ToDto(AssignmentResponse entity)
        {
            return new AssignmentResponseDTO
            {
                Id = entity.Id,
                TopicId = entity.TopicId,
                StudentId = entity.StudentId,
                Data = new AssignmentResponseData
                {
                    SubmittedAt = entity.SubmittedAt,
                    Files = entity.Files.ToList(),
                    Mark = entity.Mark,
                    Note = entity.Note,
                    TeacherFeedback = entity.TeacherFeedback,
                    GradedAt = entity.GradedAt,
                    GradedBy = entity.GradedBy
                }
            };
        }
    }
}
