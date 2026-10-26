using System.Text.Json;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Core.Shared;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.UseCases.Services.AiAssignmentGrading
{
    public sealed class AiAssignmentGradingReviewService : IAiAssignmentGradingReviewService
    {
        private const decimal MaximumScore = 10m;
        private const int MaximumTeacherGuidanceLength = 20_000;
        private const int MaximumFinalFeedbackLength = 4_000;
        private const int MaximumRejectReasonLength = 2_000;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAiAssignmentGradingJobService _jobService;

        public AiAssignmentGradingReviewService(IUnitOfWork unitOfWork, IAiAssignmentGradingJobService jobService)
        {
            _unitOfWork = unitOfWork;
            _jobService = jobService;
        }

        public async Task<AssignmentRubricResponse> UpsertRubricAsync(
            Guid topicId,
            UpsertAssignmentRubricRequest request,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var courseId = await EnsureAssignmentTopicAsync(topicId, ct);
            await EnsureTeacherCanManageCourseAsync(courseId, requesterId, requesterRole, ct);
            ValidateRubricRequest(request);

            var rubric = new AssignmentRubric
            {
                Id = Guid.NewGuid(),
                TopicId = topicId,
                Version = await _unitOfWork.AssignmentRubrics.GetNextVersionAsync(topicId, ct),
                MaxScore = request.MaxScore,
                TeacherGuidance = string.IsNullOrWhiteSpace(request.TeacherGuidance) ? null : request.TeacherGuidance.Trim(),
                CreatedBy = requesterId,
                CreatedAt = DateTime.UtcNow,
                Criteria = request.Criteria.Select(item => new AssignmentRubricCriterion
                {
                    Id = Guid.NewGuid(),
                    Name = item.Name!.Trim(),
                    Description = item.Description!.Trim(),
                    MaxScore = item.MaxScore,
                    SortOrder = item.SortOrder
                }).OrderBy(item => item.SortOrder).ToList()
            };

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await _unitOfWork.AiAssignmentGradingJobs.MarkStaleForTopicAsync(topicId, DateTime.UtcNow, token);
                    await _unitOfWork.AssignmentRubrics.AddAsync(rubric);
                    return true;
                }, ct);
            }
            catch (DbUpdateException ex)
            {
                throw new AssignmentGradingException("rubric_version_conflict", "Rubric vừa được cập nhật ở một yêu cầu khác; hãy tải lại rồi thử lại.", ex);
            }

            return MapRubric(rubric);
        }

        public async Task<AssignmentRubricResponse> GetRubricAsync(
            Guid topicId,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            var courseId = await EnsureAssignmentTopicAsync(topicId, ct);
            await EnsureTeacherCanManageCourseAsync(courseId, requesterId, requesterRole, ct);
            var rubric = await _unitOfWork.AssignmentRubrics.GetLatestByTopicIdAsync(topicId, ct)
                ?? throw new KeyNotFoundException("Rubric not found.");
            return MapRubric(rubric);
        }

        public async Task<AiAssignmentGradingJobRequestResult> RequestJobAsync(
            Guid responseId,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            var submission = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(responseId)
                ?? throw new KeyNotFoundException("Assignment response not found.");
            var courseId = await EnsureAssignmentTopicAsync(submission.TopicId, ct);
            await EnsureTeacherCanManageCourseAsync(courseId, requesterId, requesterRole, ct);
            return await _jobService.RequestAsync(responseId, requesterId, ct);
        }

        public async Task<AiAssignmentGradingJobResponse> GetJobAsync(
            Guid jobId,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            var job = await _unitOfWork.AiAssignmentGradingJobs.GetWithDraftByIdAsync(jobId, ct)
                ?? throw new KeyNotFoundException("AI grading job not found.");
            var submission = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(job.AssignmentResponseId)
                ?? throw new KeyNotFoundException("Assignment response not found.");
            var courseId = await EnsureAssignmentTopicAsync(submission.TopicId, ct);
            await EnsureTeacherCanManageCourseAsync(courseId, requesterId, requesterRole, ct);
            return MapJob(job);
        }

        public async Task<AssignmentGradingHistoryResponse> GetHistoryAsync(
            Guid responseId,
            int page,
            int pageSize,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100 || page > int.MaxValue / pageSize)
            {
                throw new ArgumentException("Page must be positive and pageSize must be between 1 and 100.");
            }
            var submission = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(responseId)
                ?? throw new KeyNotFoundException("Assignment response not found.");
            var courseId = await EnsureAssignmentTopicAsync(submission.TopicId, ct);
            await EnsureTeacherCanManageCourseAsync(courseId, requesterId, requesterRole, ct);

            var skip = checked((page - 1) * pageSize);
            var (jobs, totalCount) = await _unitOfWork.AiAssignmentGradingJobs.GetHistoryPageAsync(responseId, skip, pageSize, ct);
            return new AssignmentGradingHistoryResponse
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = jobs.Select(MapJob).ToList()
            };
        }

        public async Task<PublishedAssignmentGradeResponse> PublishDraftAsync(
            Guid draftId,
            PublishAiAssignmentGradeRequest request,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidatePublishRequest(request);
            var draft = await _unitOfWork.AiAssignmentGradeDrafts.GetTrackedByIdAsync(draftId, ct)
                ?? throw new KeyNotFoundException("AI grading draft not found.");
            var job = await _unitOfWork.AiAssignmentGradingJobs.GetTrackedByIdAsync(draft.JobId, ct)
                ?? throw new KeyNotFoundException("AI grading job not found.");
            var submission = await _unitOfWork.AssignmentResponses.GetByIdTrackedWithFilesAsync(job.AssignmentResponseId)
                ?? throw new KeyNotFoundException("Assignment response not found.");
            var courseId = await EnsureAssignmentTopicAsync(submission.TopicId, ct);
            await EnsureTeacherCanManageCourseAsync(courseId, requesterId, requesterRole, ct);
            EnsureReviewable(draft, job, request.ExpectedReviewVersion);

            try
            {
                var result = await _unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var currentSubmission = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(submission.Id);
                    if (currentSubmission == null || !await IsSnapshotCurrentAsync(currentSubmission, job, token))
                    {
                        MarkReviewStale(draft, job);
                        return (PublishedAssignmentGradeResponse?)null;
                    }
                    if (submission.GradedAt.HasValue && submission.GradedAt.Value > job.CreatedAt)
                    {
                        throw new AssignmentGradingException("grade_changed", "Điểm bài này đã được cập nhật sau khi AI tạo draft.");
                    }

                    var now = DateTime.UtcNow;
                    draft.ReviewStatus = AiAssignmentGradeReviewStatuses.Published;
                    draft.FinalScore = request.FinalScore;
                    draft.FinalFeedback = request.FinalFeedback?.Trim();
                    draft.ReviewComment = null;
                    draft.ReviewedBy = requesterId;
                    draft.ReviewedAt = now;
                    draft.ReviewVersion++;

                    submission.Mark = request.FinalScore;
                    submission.TeacherFeedback = request.FinalFeedback?.Trim();
                    submission.GradedAt = now;
                    submission.GradedBy = requesterId;
                    await _unitOfWork.LearningActivityLogs.AddAsync(new LearningActivityLog
                    {
                        Id = Guid.NewGuid(),
                        UserId = requesterId,
                        CourseId = courseId,
                        TopicId = submission.TopicId,
                        EventType = "assignment_graded",
                        EventSource = "ai_grade_publish",
                        Metadata = JsonSerializer.Serialize(new
                        {
                            assignmentResponseId = submission.Id,
                            aiGradingJobId = job.Id,
                            draftId = draft.Id,
                            mark = request.FinalScore,
                            rubricVersion = job.RubricVersion
                        }),
                        OccurredAt = now
                    });

                    return new PublishedAssignmentGradeResponse
                    {
                        AssignmentResponseId = submission.Id,
                        Score = request.FinalScore,
                        Feedback = request.FinalFeedback?.Trim(),
                        GradedAt = now
                    };
                }, ct);

                if (result == null)
                {
                    throw new AssignmentGradingException("stale_draft", "Bài nộp, đề bài hoặc rubric đã thay đổi; draft đã được đánh dấu stale.");
                }
                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new AssignmentGradingException("review_version_conflict", "Draft đã được một người khác cập nhật; hãy tải lại trước khi duyệt.", ex);
            }
        }

        public async Task RejectDraftAsync(
            Guid draftId,
            RejectAiAssignmentGradeRequest request,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.ExpectedReviewVersion < 1)
            {
                throw new ArgumentException("ExpectedReviewVersion must be positive.");
            }
            if (request.Reason?.Length > MaximumRejectReasonLength)
            {
                throw new ArgumentException($"Reason cannot exceed {MaximumRejectReasonLength} characters.");
            }

            var draft = await _unitOfWork.AiAssignmentGradeDrafts.GetTrackedByIdAsync(draftId, ct)
                ?? throw new KeyNotFoundException("AI grading draft not found.");
            var job = await _unitOfWork.AiAssignmentGradingJobs.GetTrackedByIdAsync(draft.JobId, ct)
                ?? throw new KeyNotFoundException("AI grading job not found.");
            var submission = await _unitOfWork.AssignmentResponses.GetByIdTrackedWithFilesAsync(job.AssignmentResponseId)
                ?? throw new KeyNotFoundException("Assignment response not found.");
            var courseId = await EnsureAssignmentTopicAsync(submission.TopicId, ct);
            await EnsureTeacherCanManageCourseAsync(courseId, requesterId, requesterRole, ct);
            EnsureReviewable(draft, job, request.ExpectedReviewVersion);

            try
            {
                var stale = await _unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var currentSubmission = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(submission.Id);
                    if (currentSubmission == null || !await IsSnapshotCurrentAsync(currentSubmission, job, token))
                    {
                        MarkReviewStale(draft, job);
                        return true;
                    }

                    draft.ReviewStatus = AiAssignmentGradeReviewStatuses.Rejected;
                    draft.ReviewComment = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
                    draft.ReviewedBy = requesterId;
                    draft.ReviewedAt = DateTime.UtcNow;
                    draft.ReviewVersion++;
                    return false;
                }, ct);
                if (stale)
                {
                    throw new AssignmentGradingException("stale_draft", "Bài nộp, đề bài hoặc rubric đã thay đổi; draft đã được đánh dấu stale.");
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new AssignmentGradingException("review_version_conflict", "Draft đã được một người khác cập nhật; hãy tải lại trước khi từ chối.", ex);
            }
        }

        public async Task<PublishedAssignmentGradeResponse> GetPublishedGradeAsync(
            Guid responseId,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            var submission = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(responseId)
                ?? throw new KeyNotFoundException("Assignment response not found.");
            var courseId = await EnsureAssignmentTopicAsync(submission.TopicId, ct);

            if (IsRole(requesterRole, AppRoles.Admin))
            {
                // Admin can read the final grade.
            }
            else if (IsRole(requesterRole, AppRoles.Teacher))
            {
                await EnsureTeacherCanManageCourseAsync(courseId, requesterId, requesterRole, ct);
            }
            else if (IsRole(requesterRole, AppRoles.Student))
            {
                if (submission.StudentId != requesterId)
                {
                    throw new UnauthorizedAccessException("Students can only view their own final grade.");
                }
                await EnsureStudentEnrolledAsync(requesterId, courseId, ct);
            }
            else
            {
                throw new UnauthorizedAccessException("Role is not allowed to view this grade.");
            }

            if (!submission.Mark.HasValue || !submission.GradedAt.HasValue)
            {
                throw new KeyNotFoundException("No final grade has been published for this assignment response.");
            }
            return new PublishedAssignmentGradeResponse
            {
                AssignmentResponseId = submission.Id,
                Score = submission.Mark.Value,
                Feedback = submission.TeacherFeedback,
                GradedAt = submission.GradedAt
            };
        }

        private async Task<bool> IsSnapshotCurrentAsync(AssignmentResponse submission, AiAssignmentGradingJob job, CancellationToken ct)
        {
            var rubric = await _unitOfWork.AssignmentRubrics.GetLatestByTopicIdAsync(submission.TopicId, ct);
            if (rubric == null || rubric.Id != job.RubricId || rubric.Version != job.RubricVersion) return false;
            var assignment = await _unitOfWork.TopicAssignments.GetByIdAsync(submission.TopicId, ct);
            if (assignment == null) return false;
            if (!string.Equals(assignment.Description ?? string.Empty, job.AssignmentDescriptionSnapshot, StringComparison.Ordinal)) return false;

            var currentRequestHash = AssignmentGradingInputHasher.ComputeRequestHash(
                submission,
                assignment.Description ?? string.Empty,
                AssignmentGradingInputHasher.SerializeRubric(rubric),
                job.PromptVersion,
                job.Model);
            return string.Equals(currentRequestHash, job.RequestHash, StringComparison.Ordinal);
        }

        private static void EnsureReviewable(AiAssignmentGradeDraft draft, AiAssignmentGradingJob job, int expectedReviewVersion)
        {
            if (draft.ReviewVersion != expectedReviewVersion)
            {
                throw new AssignmentGradingException("review_version_conflict", "ReviewVersion đã thay đổi; hãy tải lại draft.");
            }
            if (draft.ReviewStatus != AiAssignmentGradeReviewStatuses.Pending || job.Status != AiAssignmentGradingJobStatuses.Succeeded)
            {
                throw new AssignmentGradingException("draft_not_reviewable", "Draft không ở trạng thái có thể duyệt.");
            }
        }

        private static void MarkReviewStale(AiAssignmentGradeDraft draft, AiAssignmentGradingJob job)
        {
            draft.ReviewStatus = AiAssignmentGradeReviewStatuses.Stale;
            draft.ReviewVersion++;
            job.Status = AiAssignmentGradingJobStatuses.Stale;
            job.CompletedAt = DateTime.UtcNow;
            job.LeaseToken = null;
            job.LeaseExpiresAt = null;
        }

        private async Task<string> EnsureAssignmentTopicAsync(Guid topicId, CancellationToken ct)
        {
            var topic = await _unitOfWork.Topics.GetByIdAsync(topicId, ct)
                ?? throw new KeyNotFoundException("Assignment topic not found.");
            if (!string.Equals(topic.Type, "assignment", StringComparison.OrdinalIgnoreCase)
                || await _unitOfWork.TopicAssignments.GetByIdAsync(topicId, ct) == null)
            {
                throw new KeyNotFoundException("Assignment topic not found.");
            }
            var section = await _unitOfWork.Sections.GetByIdAsync(topic.SectionId, ct)
                ?? throw new KeyNotFoundException("Section not found.");
            _ = await _unitOfWork.Course.GetByIdAsync(section.CourseId, ct)
                ?? throw new KeyNotFoundException("Course not found.");
            return section.CourseId;
        }

        private async Task EnsureTeacherCanManageCourseAsync(string courseId, Guid requesterId, string? requesterRole, CancellationToken ct)
        {
            if (IsRole(requesterRole, AppRoles.Admin)) return;
            if (!IsRole(requesterRole, AppRoles.Teacher))
            {
                throw new UnauthorizedAccessException("Only the course teacher or an admin can manage AI grading.");
            }
            var course = await _unitOfWork.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found.");
            if (course.CreatorId != requesterId)
            {
                throw new UnauthorizedAccessException("Teachers can only manage AI grading for their own courses.");
            }
        }

        private async Task EnsureStudentEnrolledAsync(Guid studentId, string courseId, CancellationToken ct)
        {
            if (await _unitOfWork.Enrollments.GetByIdsAsync(studentId, courseId, ct) == null)
            {
                throw new UnauthorizedAccessException("Student is not enrolled in this course.");
            }
        }

        private static void ValidateRubricRequest(UpsertAssignmentRubricRequest request)
        {
            if (request.MaxScore != MaximumScore)
            {
                throw new ArgumentException("MVP rubric maxScore must equal 10.");
            }
            if (request.TeacherGuidance?.Length > MaximumTeacherGuidanceLength)
            {
                throw new ArgumentException($"Teacher guidance cannot exceed {MaximumTeacherGuidanceLength} characters.");
            }
            if (request.Criteria == null || request.Criteria.Count == 0 || request.Criteria.Count > 20)
            {
                throw new ArgumentException("Rubric must contain between 1 and 20 criteria.");
            }
            if (request.Criteria.Any(item => item == null
                || string.IsNullOrWhiteSpace(item.Name)
                || item.Name.Trim().Length > 200
                || string.IsNullOrWhiteSpace(item.Description)
                || item.Description.Trim().Length > 2000
                || item.MaxScore <= 0m
                || item.SortOrder < 0)
                || request.Criteria.Select(item => item.SortOrder).Distinct().Count() != request.Criteria.Count
                || request.Criteria.Sum(item => item.MaxScore) != request.MaxScore)
            {
                throw new ArgumentException("Rubric criteria need unique non-negative sort orders, valid descriptions and positive scores totaling 10.");
            }
        }

        private static void ValidatePublishRequest(PublishAiAssignmentGradeRequest request)
        {
            if (request.FinalScore < 0m || request.FinalScore > MaximumScore
                || decimal.Round(request.FinalScore, 2) != request.FinalScore)
            {
                throw new ArgumentException("FinalScore must be between 0 and 10 with at most two decimal places.");
            }
            if (request.ExpectedReviewVersion < 1)
            {
                throw new ArgumentException("ExpectedReviewVersion must be positive.");
            }
            if (request.FinalFeedback?.Length > MaximumFinalFeedbackLength)
            {
                throw new ArgumentException($"FinalFeedback cannot exceed {MaximumFinalFeedbackLength} characters.");
            }
        }

        private static AssignmentRubricResponse MapRubric(AssignmentRubric rubric) => new()
        {
            Id = rubric.Id,
            TopicId = rubric.TopicId,
            Version = rubric.Version,
            MaxScore = rubric.MaxScore,
            TeacherGuidance = rubric.TeacherGuidance,
            CreatedAt = rubric.CreatedAt,
            Criteria = rubric.Criteria.OrderBy(item => item.SortOrder).Select(item => new AssignmentRubricCriterionResponse
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                MaxScore = item.MaxScore,
                SortOrder = item.SortOrder
            }).ToList()
        };

        private static AiAssignmentGradingJobResponse MapJob(AiAssignmentGradingJob job) => new()
        {
            JobId = job.Id,
            AssignmentResponseId = job.AssignmentResponseId,
            Status = job.Status,
            RubricVersion = job.RubricVersion,
            Attempt = job.Attempt,
            ErrorCode = job.ErrorCode,
            ErrorMessage = job.ErrorMessage,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            Draft = job.Draft == null ? null : MapDraft(job.Draft)
        };

        private static AiAssignmentGradeDraftResponse MapDraft(AiAssignmentGradeDraft draft) => new()
        {
            DraftId = draft.Id,
            SuggestedScore = draft.SuggestedScore,
            CriterionResults = ParseJson(draft.CriterionResultsJson),
            SummaryFeedback = draft.SummaryFeedback,
            Strengths = ParseJson(draft.StrengthsJson),
            Improvements = ParseJson(draft.ImprovementsJson),
            StudySuggestions = ParseJson(draft.StudySuggestionsJson),
            ReviewStatus = draft.ReviewStatus,
            ReviewVersion = draft.ReviewVersion,
            FinalScore = draft.FinalScore,
            FinalFeedback = draft.FinalFeedback,
            ReviewComment = draft.ReviewComment,
            ReviewedBy = draft.ReviewedBy,
            ReviewedAt = draft.ReviewedAt
        };

        private static JsonElement ParseJson(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                using var document = JsonDocument.Parse("[]");
                return document.RootElement.Clone();
            }
        }

        private static bool IsRole(string? actual, string expected) =>
            expected == AppRoles.Student
                ? AppRoles.IsStudent(actual)
                : string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
