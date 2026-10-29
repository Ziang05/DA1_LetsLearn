using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Core.Shared;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LetsLearn.UseCases.Services
{
    public class LearningProgressService : ILearningProgressService
    {
        private const string StatusNotStarted = "not_started";
        private const string StatusInProgress = "in_progress";
        private const string StatusCompleted = "completed";
        private const string SourceViewTopic = "view_topic";
        private const string SourceSubmitQuiz = "submit_quiz";
        private const string SourceSubmitAssignment = "submit_assignment";
        private const string EventTopicViewed = "topic_viewed";
        private const string EventTopicCompleted = "topic_completed";

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<LearningProgressService> _logger;

        public LearningProgressService(IUnitOfWork unitOfWork, ILogger<LearningProgressService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<LearningProgressSummaryDto> GetMyCourseProgressAsync(string courseId, Guid studentId, CancellationToken ct = default)
        {
            ValidateCourseId(courseId);
            await EnsureStudentCanTrackCourseAsync(courseId, studentId, ct);
            return await RecalculateCourseProgressAsync(courseId, studentId, ct);
        }

        public async Task<List<TopicProgressDto>> GetMyTopicProgressesAsync(string courseId, Guid studentId, CancellationToken ct = default)
        {
            ValidateCourseId(courseId);
            await EnsureStudentCanTrackCourseAsync(courseId, studentId, ct);

            var topics = await GetCourseTopicsAsync(courseId, ct);
            var progresses = await _unitOfWork.TopicProgresses.GetByStudentAndCourseAsync(studentId, courseId, ct);
            var progressByTopicId = progresses.ToDictionary(p => p.TopicId);

            return topics
                .OrderBy(t => t.SectionId)
                .ThenBy(t => t.Title)
                .Select(topic =>
                {
                    progressByTopicId.TryGetValue(topic.Id, out var progress);
                    return MapTopicProgress(progress, topic, courseId, studentId);
                })
                .ToList();
        }

        public async Task<TopicProgressDto> MarkTopicViewedAsync(Guid topicId, Guid studentId, CancellationToken ct = default)
        {
            var (topic, courseId) = await ResolveTopicCourseAsync(topicId, ct);
            await EnsureStudentCanTrackCourseAsync(courseId, studentId, ct);

            var now = DateTime.UtcNow;
            var progress = await _unitOfWork.TopicProgresses.GetByStudentAndTopicAsync(studentId, topicId, ct);

            if (progress == null)
            {
                progress = new TopicProgress
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    CourseId = courseId,
                    TopicId = topicId,
                    Status = StatusInProgress,
                    FirstAccessedAt = now,
                    LastAccessedAt = now,
                    CompletionSource = SourceViewTopic,
                    UpdatedAt = now
                };

                await _unitOfWork.TopicProgresses.AddAsync(progress);
            }
            else
            {
                if (progress.Status != StatusCompleted)
                {
                    progress.Status = StatusInProgress;
                    progress.CompletionSource ??= SourceViewTopic;
                }

                progress.FirstAccessedAt ??= now;
                progress.LastAccessedAt = now;
                progress.UpdatedAt = now;
                await _unitOfWork.TopicProgresses.UpdateAsync(progress);
            }

            await AddActivityLogAsync(studentId, courseId, topicId, EventTopicViewed, SourceViewTopic, new
            {
                topic.Title,
                topic.Type
            });
            await _unitOfWork.CommitAsync();
            await RecalculateCourseProgressAsync(courseId, studentId, ct);

            _logger.LogInformation("Marked topic {TopicId} as viewed for student {StudentId}.", topicId, studentId);
            return MapTopicProgress(progress, topic, courseId, studentId);
        }

        public async Task<TopicProgressDto> MarkTopicCompletedAsync(Guid topicId, Guid studentId, string completionSource, CancellationToken ct = default)
        {
            var (topic, courseId) = await ResolveTopicCourseAsync(topicId, ct);
            await EnsureStudentCanTrackCourseAsync(courseId, studentId, ct);

            var normalizedSource = NormalizeCompletionSource(completionSource);
            var now = DateTime.UtcNow;
            var progress = await _unitOfWork.TopicProgresses.GetByStudentAndTopicAsync(studentId, topicId, ct);

            if (progress == null)
            {
                progress = new TopicProgress
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    CourseId = courseId,
                    TopicId = topicId,
                    Status = StatusCompleted,
                    FirstAccessedAt = now,
                    LastAccessedAt = now,
                    CompletedAt = now,
                    CompletionSource = normalizedSource,
                    UpdatedAt = now
                };

                await _unitOfWork.TopicProgresses.AddAsync(progress);
            }
            else
            {
                progress.Status = StatusCompleted;
                progress.FirstAccessedAt ??= now;
                progress.LastAccessedAt = now;
                progress.CompletedAt ??= now;
                progress.CompletionSource = normalizedSource;
                progress.UpdatedAt = now;
                await _unitOfWork.TopicProgresses.UpdateAsync(progress);
            }

            await AddActivityLogAsync(studentId, courseId, topicId, EventTopicCompleted, normalizedSource, new
            {
                topic.Title,
                topic.Type
            });
            await _unitOfWork.CommitAsync();
            await RecalculateCourseProgressAsync(courseId, studentId, ct);

            _logger.LogInformation(
                "Marked topic {TopicId} as completed for student {StudentId}. Source={CompletionSource}",
                topicId,
                studentId,
                normalizedSource);

            return MapTopicProgress(progress, topic, courseId, studentId);
        }

        public async Task<LearningProgressSummaryDto> RecalculateCourseProgressAsync(string courseId, Guid studentId, CancellationToken ct = default)
        {
            ValidateCourseId(courseId);
            var enrollment = await EnsureStudentCanTrackCourseAsync(courseId, studentId, ct);
            var topics = await GetCourseTopicsAsync(courseId, ct);
            var totalTopicCount = topics.Count;
            var completedTopicCount = totalTopicCount == 0
                ? 0
                : await _unitOfWork.TopicProgresses.CountCompletedByStudentAndCourseAsync(studentId, courseId, ct);

            completedTopicCount = Math.Min(completedTopicCount, totalTopicCount);
            var progressPercent = totalTopicCount == 0
                ? 0m
                : Math.Round((decimal)completedTopicCount / totalTopicCount * 100m, 2);
            var topicProgresses = await _unitOfWork.TopicProgresses.GetByStudentAndCourseAsync(studentId, courseId, ct);
            var lastActivityAt = topicProgresses
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => (DateTime?)p.UpdatedAt)
                .FirstOrDefault();
            var now = DateTime.UtcNow;
            var progress = await _unitOfWork.LearningProgresses.GetByStudentAndCourseAsync(studentId, courseId, ct);
            var isNewProgress = progress == null;

            if (isNewProgress)
            {
                progress = new LearningProgress
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    CourseId = courseId,
                    StartedAt = enrollment.JoinDate,
                    UpdatedAt = now
                };

                await _unitOfWork.LearningProgresses.AddAsync(progress);
            }

            progress.CompletedTopicCount = completedTopicCount;
            progress.TotalTopicCount = totalTopicCount;
            progress.ProgressPercent = progressPercent;
            progress.LastActivityAt = lastActivityAt;
            progress.StartedAt ??= enrollment.JoinDate;
            progress.CompletedAt = totalTopicCount > 0 && completedTopicCount == totalTopicCount
                ? progress.CompletedAt ?? now
                : null;
            progress.UpdatedAt = now;

            if (!isNewProgress)
            {
                await _unitOfWork.LearningProgresses.UpdateAsync(progress);
            }

            await _unitOfWork.CommitAsync();
            return MapLearningProgress(progress);
        }

        public async Task<List<CourseStudentProgressDto>> GetCourseStudentProgressesAsync(
            string courseId,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            ValidateCourseId(courseId);
            await EnsureCanInspectCourseAsync(courseId, requesterId, requesterRole, ct);

            var enrollments = await _unitOfWork.Enrollments.GetAllByCourseIdAsync(courseId, ct);
            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
            var progressByStudentId = (await _unitOfWork.LearningProgresses.GetByCourseAsync(courseId, ct))
                .ToDictionary(p => p.StudentId);
            var students = studentIds.Count == 0
                ? new List<User>()
                : (await _unitOfWork.Users.FindAsync(u => studentIds.Contains(u.Id), ct))
                    .Where(u => u != null)
                    .Cast<User>()
                    .ToList();
            var studentById = students.ToDictionary(s => s.Id);
            var results = new List<CourseStudentProgressDto>();

            foreach (var enrollment in enrollments.OrderBy(e => e.JoinDate))
            {
                if (!progressByStudentId.TryGetValue(enrollment.StudentId, out var progress))
                {
                    var recalculated = await RecalculateCourseProgressAsync(courseId, enrollment.StudentId, ct);
                    progress = new LearningProgress
                    {
                        Id = recalculated.Id,
                        StudentId = recalculated.StudentId,
                        CourseId = recalculated.CourseId,
                        CompletedTopicCount = recalculated.CompletedTopicCount,
                        TotalTopicCount = recalculated.TotalTopicCount,
                        ProgressPercent = recalculated.ProgressPercent,
                        StartedAt = recalculated.StartedAt,
                        CompletedAt = recalculated.CompletedAt,
                        LastActivityAt = recalculated.LastActivityAt,
                        UpdatedAt = recalculated.UpdatedAt
                    };
                }

                studentById.TryGetValue(enrollment.StudentId, out var student);
                results.Add(MapCourseStudentProgress(progress, enrollment, student));
            }

            return results
                .OrderByDescending(p => p.ProgressPercent)
                .ThenByDescending(p => p.LastActivityAt)
                .ThenBy(p => p.StudentName)
                .ToList();
        }

        public async Task<List<TopicProgressDto>> GetStudentTopicProgressesForCourseAsync(
            string courseId,
            Guid studentId,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            ValidateCourseId(courseId);
            await EnsureCanInspectCourseAsync(courseId, requesterId, requesterRole, ct);
            await EnsureStudentCanTrackCourseAsync(courseId, studentId, ct);

            var topics = await GetCourseTopicsAsync(courseId, ct);
            var progresses = await _unitOfWork.TopicProgresses.GetByStudentAndCourseAsync(studentId, courseId, ct);
            var progressByTopicId = progresses.ToDictionary(p => p.TopicId);

            return topics
                .OrderBy(t => t.SectionId)
                .ThenBy(t => t.Title)
                .Select(topic =>
                {
                    progressByTopicId.TryGetValue(topic.Id, out var progress);
                    return MapTopicProgress(progress, topic, courseId, studentId);
                })
                .ToList();
        }

        public async Task<LearningProgressSyncResultDto> SyncCourseProgressFromResponsesAsync(
            string courseId,
            Guid requesterId,
            string? requesterRole,
            CancellationToken ct = default)
        {
            ValidateCourseId(courseId);
            await EnsureCanInspectCourseAsync(courseId, requesterId, requesterRole, ct);

            var now = DateTime.UtcNow;
            var topics = await GetCourseTopicsAsync(courseId, ct);
            var topicIds = topics.Select(t => t.Id).ToList();
            var enrollments = await _unitOfWork.Enrollments.GetAllByCourseIdAsync(courseId, ct);
            var result = new LearningProgressSyncResultDto
            {
                CourseId = courseId,
                StudentCount = enrollments.Count,
                SyncedAt = now
            };

            if (topicIds.Count == 0 || enrollments.Count == 0)
            {
                return result;
            }

            foreach (var enrollment in enrollments)
            {
                var completions = await GetResponseCompletionsAsync(topicIds, enrollment.StudentId, now, ct);

                foreach (var completion in completions.Values)
                {
                    var progress = await _unitOfWork.TopicProgresses.GetByStudentAndTopicAsync(
                        enrollment.StudentId,
                        completion.TopicId,
                        ct);

                    if (progress == null)
                    {
                        progress = new TopicProgress
                        {
                            Id = Guid.NewGuid(),
                            StudentId = enrollment.StudentId,
                            CourseId = courseId,
                            TopicId = completion.TopicId,
                            Status = StatusCompleted,
                            FirstAccessedAt = completion.CompletedAt,
                            LastAccessedAt = completion.CompletedAt,
                            CompletedAt = completion.CompletedAt,
                            CompletionSource = completion.Source,
                            UpdatedAt = now
                        };

                        await _unitOfWork.TopicProgresses.AddAsync(progress);
                        result.CreatedTopicProgressCount++;
                    }
                    else if (progress.Status != StatusCompleted)
                    {
                        progress.Status = StatusCompleted;
                        progress.FirstAccessedAt ??= completion.CompletedAt;
                        progress.LastAccessedAt = completion.CompletedAt;
                        progress.CompletedAt ??= completion.CompletedAt;
                        progress.CompletionSource = completion.Source;
                        progress.UpdatedAt = now;

                        await _unitOfWork.TopicProgresses.UpdateAsync(progress);
                        result.UpdatedTopicProgressCount++;
                    }
                }

                if (completions.Count > 0)
                {
                    await _unitOfWork.CommitAsync();
                }

                await RecalculateCourseProgressAsync(courseId, enrollment.StudentId, ct);
                result.RecalculatedProgressCount++;
            }

            _logger.LogInformation(
                "Synced learning progress for course {CourseId}. Students={StudentCount}, Created={CreatedCount}, Updated={UpdatedCount}",
                courseId,
                result.StudentCount,
                result.CreatedTopicProgressCount,
                result.UpdatedTopicProgressCount);

            return result;
        }

        private async Task<Enrollment> EnsureStudentCanTrackCourseAsync(string courseId, Guid studentId, CancellationToken ct)
        {
            _ = await _unitOfWork.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found.");

            return await _unitOfWork.Enrollments.GetByIdsAsync(studentId, courseId, ct)
                ?? throw new UnauthorizedAccessException("Student is not enrolled in this course.");
        }

        private async Task EnsureCanInspectCourseAsync(string courseId, Guid requesterId, string? requesterRole, CancellationToken ct)
        {
            var course = await _unitOfWork.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found.");

            if (string.Equals(requesterRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(requesterRole, AppRoles.Teacher, StringComparison.OrdinalIgnoreCase)
                && course.CreatorId == requesterId)
            {
                return;
            }

            throw new UnauthorizedAccessException("Only course teachers or admins can inspect course progress.");
        }

        private async Task<(Topic Topic, string CourseId)> ResolveTopicCourseAsync(Guid topicId, CancellationToken ct)
        {
            var topic = await _unitOfWork.Topics.GetByIdAsync(topicId, ct)
                ?? throw new KeyNotFoundException("Topic not found.");
            var section = await _unitOfWork.Sections.GetByIdAsync(topic.SectionId, ct)
                ?? throw new KeyNotFoundException("Section not found.");

            return (topic, section.CourseId);
        }

        private async Task<List<Topic>> GetCourseTopicsAsync(string courseId, CancellationToken ct)
        {
            var sections = (await _unitOfWork.Sections.FindAsync(s => s.CourseId == courseId, ct))
                .Where(s => s != null)
                .Cast<Section>()
                .ToList();

            if (sections.Count == 0)
            {
                return new List<Topic>();
            }

            return (await _unitOfWork.Topics.GetAllBySectionIdsAsync(sections.Select(s => s.Id).ToList(), ct)).ToList();
        }

        private static LearningProgressSummaryDto MapLearningProgress(LearningProgress progress)
        {
            return new LearningProgressSummaryDto
            {
                Id = progress.Id,
                StudentId = progress.StudentId,
                CourseId = progress.CourseId,
                CompletedTopicCount = progress.CompletedTopicCount,
                TotalTopicCount = progress.TotalTopicCount,
                ProgressPercent = progress.ProgressPercent,
                StartedAt = progress.StartedAt,
                CompletedAt = progress.CompletedAt,
                LastActivityAt = progress.LastActivityAt,
                UpdatedAt = progress.UpdatedAt
            };
        }

        private static CourseStudentProgressDto MapCourseStudentProgress(LearningProgress progress, Enrollment enrollment, User? student)
        {
            return new CourseStudentProgressDto
            {
                Id = progress.Id,
                StudentId = progress.StudentId,
                CourseId = progress.CourseId,
                CompletedTopicCount = progress.CompletedTopicCount,
                TotalTopicCount = progress.TotalTopicCount,
                ProgressPercent = progress.ProgressPercent,
                StartedAt = progress.StartedAt,
                CompletedAt = progress.CompletedAt,
                LastActivityAt = progress.LastActivityAt,
                UpdatedAt = progress.UpdatedAt,
                JoinedAt = enrollment.JoinDate,
                StudentName = student?.Username,
                StudentEmail = student?.Email,
                StudentAvatar = student?.Avatar
            };
        }

        private static TopicProgressDto MapTopicProgress(TopicProgress? progress, Topic topic, string courseId, Guid studentId)
        {
            return new TopicProgressDto
            {
                Id = progress?.Id ?? Guid.Empty,
                StudentId = progress?.StudentId ?? studentId,
                CourseId = progress?.CourseId ?? courseId,
                TopicId = topic.Id,
                TopicTitle = topic.Title,
                TopicType = topic.Type,
                Status = progress?.Status ?? StatusNotStarted,
                FirstAccessedAt = progress?.FirstAccessedAt,
                LastAccessedAt = progress?.LastAccessedAt,
                CompletedAt = progress?.CompletedAt,
                CompletionSource = progress?.CompletionSource,
                UpdatedAt = progress?.UpdatedAt ?? default
            };
        }

        private static string NormalizeCompletionSource(string completionSource)
        {
            if (string.IsNullOrWhiteSpace(completionSource))
            {
                return "manual";
            }

            var normalized = string.Join("_", completionSource
                .Trim()
                .ToLowerInvariant()
                .Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries));

            return normalized.Length <= 50 ? normalized : normalized[..50];
        }

        private async Task<Dictionary<Guid, ResponseCompletion>> GetResponseCompletionsAsync(
            List<Guid> topicIds,
            Guid studentId,
            DateTime fallbackTime,
            CancellationToken ct)
        {
            var completions = new Dictionary<Guid, ResponseCompletion>();
            var quizResponses = await _unitOfWork.QuizResponses.FindByTopicIdsAndStudentIdAsync(topicIds, studentId, ct);
            var assignmentResponses = await _unitOfWork.AssignmentResponses.FindByTopicIdsAndStudentIdAsync(topicIds, studentId, ct);

            foreach (var response in quizResponses)
            {
                AddCompletion(
                    completions,
                    response.TopicId,
                    SourceSubmitQuiz,
                    response.CompletedAt ?? response.StartedAt ?? fallbackTime);
            }

            foreach (var response in assignmentResponses)
            {
                AddCompletion(
                    completions,
                    response.TopicId,
                    SourceSubmitAssignment,
                    response.SubmittedAt ?? fallbackTime);
            }

            return completions;
        }

        private static void AddCompletion(
            Dictionary<Guid, ResponseCompletion> completions,
            Guid topicId,
            string source,
            DateTime completedAt)
        {
            if (!completions.TryGetValue(topicId, out var existing) || completedAt >= existing.CompletedAt)
            {
                completions[topicId] = new ResponseCompletion(topicId, source, completedAt);
            }
        }

        private async Task AddActivityLogAsync(
            Guid userId,
            string? courseId,
            Guid? topicId,
            string eventType,
            string? eventSource,
            object? metadata = null)
        {
            await _unitOfWork.LearningActivityLogs.AddAsync(new LearningActivityLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CourseId = courseId,
                TopicId = topicId,
                EventType = eventType,
                EventSource = eventSource,
                Metadata = metadata == null ? null : JsonSerializer.Serialize(metadata),
                OccurredAt = DateTime.UtcNow
            });
        }

        private static void ValidateCourseId(string courseId)
        {
            if (string.IsNullOrWhiteSpace(courseId))
            {
                throw new ArgumentException("CourseId is required.", nameof(courseId));
            }
        }

        private sealed record ResponseCompletion(Guid TopicId, string Source, DateTime CompletedAt);
    }
}
