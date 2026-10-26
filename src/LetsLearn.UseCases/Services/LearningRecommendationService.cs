using System.Text.Json;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;

namespace LetsLearn.UseCases.Services
{
    public sealed class LearningRecommendationService : ILearningRecommendationService
    {
        private const decimal DefaultWeakScoreThreshold = 0.6m;
        private readonly IUnitOfWork _unitOfWork;

        public LearningRecommendationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<LearningRecommendationsResponseDto> GetMyRecommendationsAsync(
            string courseId,
            Guid studentId,
            int limit = 3,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(courseId))
            {
                throw new ArgumentException("CourseId is required.", nameof(courseId));
            }

            if (limit < 1 || limit > 10)
            {
                throw new ArgumentException("Limit must be between 1 and 10.", nameof(limit));
            }

            _ = await _unitOfWork.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found.");
            _ = await _unitOfWork.Enrollments.GetByIdsAsync(studentId, courseId, ct)
                ?? throw new UnauthorizedAccessException("Student is not enrolled in this course.");

            var sections = (await _unitOfWork.Sections.FindAsync(section => section.CourseId == courseId, ct))
                .Where(section => section != null)
                .Cast<Section>()
                .OrderBy(section => section.Position ?? int.MaxValue)
                .ThenBy(section => section.Title)
                .ToList();

            var sectionIds = sections.Select(section => section.Id).ToList();
            var topics = sectionIds.Count == 0
                ? new List<Topic>()
                : (await _unitOfWork.Topics.GetAllBySectionIdsAsync(sectionIds, ct)).ToList();
            var topicIds = topics.Select(topic => topic.Id).ToList();
            var sectionById = sections.ToDictionary(section => section.Id);
            var topicById = topics.ToDictionary(topic => topic.Id);

            var progressByTopicId = (await _unitOfWork.TopicProgresses.GetByStudentAndCourseAsync(studentId, courseId, ct))
                .ToDictionary(progress => progress.TopicId);

            var quizTopics = topics
                .Where(topic => IsType(topic.Type, "quiz"))
                .Select(topic => topic.Id)
                .ToList();
            Dictionary<Guid, TopicQuiz> quizzes = quizTopics.Count == 0
                ? new Dictionary<Guid, TopicQuiz>()
                : (await _unitOfWork.TopicQuizzes.FindAsync(quiz => quizTopics.Contains(quiz.TopicId), ct))
                    .Where(quiz => quiz != null)
                    .Cast<TopicQuiz>()
                    .ToDictionary(quiz => quiz.TopicId);

            var quizAttempts = topicIds.Count == 0
                ? new List<QuizResponse>()
                : await _unitOfWork.QuizResponses.FindByTopicIdsAndStudentIdAsync(topicIds, studentId, ct);

            var validQuizAttempts = quizAttempts
                .Where(attempt => quizTopics.Contains(attempt.TopicId) && topicById.ContainsKey(attempt.TopicId))
                .Select(attempt => new
                {
                    Attempt = attempt,
                    Score = CalculateTrustedQuizScore(attempt)
                })
                .Where(item => item.Score != null)
                .ToList();

            var scoredQuizTopics = validQuizAttempts
                .GroupBy(item => item.Attempt.TopicId)
                .Select(group =>
                {
                    var attempts = group
                        .OrderBy(item => item.Attempt.CompletedAt)
                        .TakeLast(3)
                        .ToList();
                    var weightedScore = WeightedRecentScore(attempts.Select(item => item.Score!.Percent).ToList());
                    var latest = attempts[^1].Score!;
                    var passThreshold = GetPassThreshold(
                        quizzes.GetValueOrDefault(group.Key),
                        latest.TotalMark);
                    var recentResults = attempts.Select(item =>
                    {
                        var score = item.Score!;
                        return new LearningScoreEvidenceDto
                        {
                            RecordedAtUtc = item.Attempt.CompletedAt,
                            ScorePercent = Math.Round(score.Percent, 1),
                            EarnedMark = Math.Round(score.Percent * score.TotalMark / 100m, 2),
                            MaximumMark = Math.Round(score.TotalMark, 2)
                        };
                    }).ToList();

                    return new WeakTopic(
                        group.Key,
                        "quiz",
                        weightedScore,
                        passThreshold,
                        attempts.Count,
                        group.Max(item => item.Attempt.CompletedAt) ?? DateTime.MinValue,
                        recentResults);
                })
                .Where(result => result.ScorePercent < result.PassThreshold * 100m)
                .OrderBy(result => result.ScorePercent / Math.Max(result.PassThreshold, 0.01m))
                .ThenByDescending(result => result.LastAttemptAt)
                .ToList();

            var assignmentResponses = topicIds.Count == 0
                ? Array.Empty<AssignmentResponse>()
                : (await _unitOfWork.AssignmentResponses.FindByTopicIdsAndStudentIdAsync(topicIds, studentId, ct)).ToArray();
            var assignmentTopics = assignmentResponses.Select(response => response.TopicId).Distinct().ToList();
            var rubrics = assignmentTopics.Count == 0
                ? new List<AssignmentRubric>()
                : (await _unitOfWork.AssignmentRubrics.FindAsync(rubric => assignmentTopics.Contains(rubric.TopicId), ct))
                    .Where(rubric => rubric != null)
                    .Cast<AssignmentRubric>()
                    .ToList();

            var analyzedAssignmentTopics = assignmentResponses
                .Where(response => response.Mark.HasValue
                    && response.GradedAt.HasValue
                    && topicById.TryGetValue(response.TopicId, out var topic)
                    && IsType(topic.Type, "assignment"))
                .Select(response =>
                {
                    var applicableRubric = rubrics
                        .Where(rubric => rubric.TopicId == response.TopicId && rubric.CreatedAt <= response.GradedAt!.Value)
                        .OrderByDescending(rubric => rubric.Version)
                        .FirstOrDefault();
                    if (applicableRubric == null || applicableRubric.MaxScore <= 0m)
                    {
                        return null;
                    }

                    return new
                    {
                        Response = response,
                        ScorePercent = Math.Clamp(response.Mark!.Value / applicableRubric.MaxScore, 0m, 1m) * 100m,
                        EarnedMark = response.Mark.Value,
                        MaximumMark = applicableRubric.MaxScore
                    };
                })
                .Where(item => item != null && topicById.ContainsKey(item.Response.TopicId))
                .GroupBy(item => item!.Response.TopicId)
                .Select(group =>
                {
                    var attempts = group
                        .OrderBy(item => item!.Response.GradedAt)
                        .TakeLast(3)
                        .ToList();
                    var weightedScore = WeightedRecentScore(attempts.Select(item => item!.ScorePercent).ToList());
                    var latest = attempts[^1]!;
                    var recentResults = attempts.Select(item => new LearningScoreEvidenceDto
                    {
                        RecordedAtUtc = item!.Response.GradedAt,
                        ScorePercent = Math.Round(item.ScorePercent, 1),
                        EarnedMark = Math.Round(item.EarnedMark, 2),
                        MaximumMark = Math.Round(item.MaximumMark, 2)
                    }).ToList();
                    return new WeakTopic(
                        group.Key,
                        "assignment",
                        weightedScore,
                        DefaultWeakScoreThreshold,
                        attempts.Count,
                        latest.Response.GradedAt!.Value,
                        recentResults);
                })
                .ToList();

            var scoredAssignmentTopics = analyzedAssignmentTopics
                .Where(result => result.ScorePercent < result.PassThreshold * 100m)
                .ToList();

            var weakTopics = scoredQuizTopics
                .Concat(scoredAssignmentTopics)
                .OrderBy(result => result.ScorePercent / Math.Max(result.PassThreshold, 0.01m))
                .ThenByDescending(result => result.LastAttemptAt)
                .ToList();

            var recommendations = new List<LearningRecommendationDto>();

            foreach (var weakTopic in weakTopics)
            {
                if (!topicById.TryGetValue(weakTopic.TopicId, out var topic))
                {
                    continue;
                }

                if (recommendations.Any(item => IsSameRecommendation(item, topic)))
                {
                    continue;
                }

                recommendations.Add(new LearningRecommendationDto
                {
                    TopicId = topic.Id,
                    Title = topic.Title ?? "Quiz",
                    TopicType = topic.Type ?? "quiz",
                    Reason = weakTopic.TopicType == "quiz"
                        ? $"Điểm các lần làm bài kiểm tra gần đây là {Math.Round(weakTopic.ScorePercent)}%, thấp hơn mốc cần đạt {Math.Round(weakTopic.PassThreshold * 100m)}%. Bạn nên ôn lại nội dung này trước khi làm lại."
                        : $"Điểm bài tập gần đây là {Math.Round(weakTopic.ScorePercent)}%, thấp hơn mức gợi ý 60%. Bạn nên xem nhận xét của giáo viên và ôn lại phần liên quan.",
                    EvidenceLabel = weakTopic.TopicType == "quiz"
                        ? $"{Math.Round(weakTopic.ScorePercent)}% · {weakTopic.AttemptCount} lần gần đây"
                        : $"{Math.Round(weakTopic.ScorePercent)}% · đã được chấm",
                    ScorePercent = Math.Round(weakTopic.ScorePercent, 1),
                    PassThresholdPercent = Math.Round(weakTopic.PassThreshold * 100m, 1),
                    AttemptCount = weakTopic.AttemptCount,
                    SectionTitle = sectionById.GetValueOrDefault(topic.SectionId)?.Title,
                    ObservedProgressStatus = progressByTopicId.GetValueOrDefault(topic.Id)?.Status,
                    LastAccessedAtUtc = progressByTopicId.GetValueOrDefault(topic.Id)?.LastAccessedAt,
                    RecentResults = weakTopic.RecentResults,
                    AnalysisBasis = weakTopic.TopicType == "quiz"
                        ? "Hệ thống xem tối đa 3 lần làm gần nhất và chú ý nhiều hơn đến kết quả mới nhất."
                        : "Hệ thống xem tối đa 3 bài tập gần nhất đã được chấm và so điểm với tiêu chí chấm của bài.",
                    Priority = "high",
                    ActionLabel = weakTopic.TopicType == "quiz" ? "Ôn và làm lại" : "Xem nhận xét"
                });

                if (weakTopic.TopicType == "quiz")
                {
                    // Topics do not yet have explicit skill/resource mappings. Offer at most one
                    // unfinished resource from the same section and describe it as a section-level aid.
                    var sectionResource = topics
                        .Where(candidate => candidate.SectionId == topic.SectionId
                            && IsReviewResource(candidate.Type)
                            && !IsCompleted(progressByTopicId.GetValueOrDefault(candidate.Id))
                            && recommendations.All(item => item.TopicId != candidate.Id))
                        .OrderBy(candidate => ResourcePriority(candidate.Type))
                        .ThenBy(candidate => candidate.Title)
                        .FirstOrDefault();

                    if (sectionResource != null && recommendations.Count < limit)
                    {
                        recommendations.Add(new LearningRecommendationDto
                        {
                            TopicId = sectionResource.Id,
                            Title = sectionResource.Title ?? "Tài liệu ôn tập",
                            TopicType = sectionResource.Type ?? "page",
                            Reason = "Tài liệu chưa hoàn thành trong cùng phần có thể giúp bạn ôn lại trước khi làm quiz.",
                            EvidenceLabel = "Tài liệu cùng phần",
                            AnalysisBasis = "Tài liệu này nằm cùng phần với bài kiểm tra cần ôn và bạn chưa hoàn thành.",
                            SectionTitle = sectionById.GetValueOrDefault(sectionResource.SectionId)?.Title,
                            ObservedProgressStatus = progressByTopicId.GetValueOrDefault(sectionResource.Id)?.Status,
                            LastAccessedAtUtc = progressByTopicId.GetValueOrDefault(sectionResource.Id)?.LastAccessedAt,
                            Priority = "normal",
                            ActionLabel = "Mở tài liệu"
                        });
                    }
                }

                if (recommendations.Count >= limit)
                {
                    break;
                }
            }

            if (recommendations.Count == 0)
            {
                var nextTopic = topics
                    .Where(topic => IsActionable(topic.Type)
                        && !IsCompleted(progressByTopicId.GetValueOrDefault(topic.Id)))
                    .OrderBy(topic => sectionById.GetValueOrDefault(topic.SectionId)?.Position ?? int.MaxValue)
                    .ThenBy(topic => IsStarted(progressByTopicId.GetValueOrDefault(topic.Id)) ? 0 : 1)
                    .ThenBy(topic => IsStarted(progressByTopicId.GetValueOrDefault(topic.Id))
                        ? progressByTopicId.GetValueOrDefault(topic.Id)?.LastAccessedAt ?? DateTime.MaxValue
                        : DateTime.MaxValue)
                    .ThenBy(topic => ResourcePriority(topic.Type))
                    .ThenBy(topic => topic.Title)
                    .Take(limit)
                    .ToList();

                foreach (var topic in nextTopic)
                {
                    if (recommendations.Any(item => IsSameRecommendation(item, topic)))
                    {
                        continue;
                    }

                    var inProgress = IsStarted(progressByTopicId.GetValueOrDefault(topic.Id));
                    recommendations.Add(new LearningRecommendationDto
                    {
                        TopicId = topic.Id,
                        Title = topic.Title ?? "Nội dung khóa học",
                        TopicType = topic.Type ?? "page",
                        Reason = inProgress
                            ? "Bạn đã bắt đầu nội dung này nhưng chưa đánh dấu hoàn thành."
                            : "Nội dung này chưa được bắt đầu trong khóa học.",
                        EvidenceLabel = inProgress ? "Đang học" : "Chưa bắt đầu",
                        AnalysisBasis = inProgress
                            ? "Bạn đã mở nội dung này nhưng chưa hoàn thành."
                            : "Bạn chưa bắt đầu nội dung này; đây là bước tiếp theo trong khóa học, không có nghĩa là bạn đang yếu phần này.",
                        SectionTitle = sectionById.GetValueOrDefault(topic.SectionId)?.Title,
                        ObservedProgressStatus = progressByTopicId.GetValueOrDefault(topic.Id)?.Status ?? "not_started",
                        LastAccessedAtUtc = progressByTopicId.GetValueOrDefault(topic.Id)?.LastAccessedAt,
                        Priority = inProgress ? "high" : "normal",
                        ActionLabel = inProgress ? "Học tiếp" : "Bắt đầu học"
                    });
                }
            }

            var summary = recommendations.Count > 0
                ? "Dựa trên tiến độ và kết quả học tập trong khóa học này."
                : topics.Count == 0
                    ? "Khóa học chưa có nội dung để đề xuất."
                    : "Bạn đã hoàn thành các nội dung hiện có; hiện chưa có điểm quiz nào cần ưu tiên ôn tập.";

            return new LearningRecommendationsResponseDto
            {
                CourseId = courseId,
                GeneratedAtUtc = DateTime.UtcNow,
                Summary = summary,
                Analysis = new LearningAnalysisEvidenceDto
                {
                    TopicsInCourse = topics.Count,
                    CompletedTopics = progressByTopicId.Values.Count(IsCompleted),
                    QuizAttemptsFound = quizAttempts.Count,
                    QuizAttemptsReviewed = validQuizAttempts.Count,
                    AssignmentSubmissionsFound = assignmentResponses.Length,
                    GradedAssignmentsReviewed = analyzedAssignmentTopics.Sum(result => result.AttemptCount),
                    WeakTopicsFound = weakTopics.Count,
                    Method = "Với bài kiểm tra, hệ thống xem tối đa 3 lần làm gần nhất và chú ý nhiều hơn đến kết quả mới. Với bài tập, hệ thống chỉ xem những bài đã được chấm. Nội dung có điểm thấp hơn mốc cần đạt sẽ được ưu tiên gợi ý để bạn ôn lại."
                },
                Items = recommendations.Take(limit).ToList()
            };
        }

        private static QuizScore? CalculateTrustedQuizScore(QuizResponse attempt)
        {
            if (!string.Equals(attempt.Status, "Finished", StringComparison.OrdinalIgnoreCase)
                || !attempt.CompletedAt.HasValue
                || attempt.Answers.Count == 0)
            {
                return null;
            }

            decimal earned = 0m;
            decimal total = 0m;

            foreach (var answer in attempt.Answers)
            {
                if (string.IsNullOrWhiteSpace(answer.Question))
                {
                    return null;
                }

                Question? question;
                try
                {
                    question = JsonSerializer.Deserialize<Question>(answer.Question);
                }
                catch (JsonException)
                {
                    return null;
                }

                if (question?.DefaultMark is not decimal maxMark || maxMark <= 0m)
                {
                    return null;
                }

                var answerMark = QuizAnswerScorer.CalculateMark(question, answer.Answer);
                if (answerMark is null)
                {
                    return null;
                }

                total += maxMark;
                earned += Math.Clamp(answerMark.Value, 0m, maxMark);
            }

            return total <= 0m
                ? null
                : new QuizScore(Math.Clamp(earned / total, 0m, 1m) * 100m, total);
        }

        private static decimal GetPassThreshold(TopicQuiz? quiz, decimal totalMark)
        {
            if (quiz?.GradeToPass is decimal passMark && passMark > 0m && totalMark > 0m && passMark <= totalMark)
            {
                return Math.Clamp(passMark / totalMark, 0.01m, 1m);
            }

            return DefaultWeakScoreThreshold;
        }

        private static decimal WeightedRecentScore(IReadOnlyList<decimal> scores)
        {
            decimal weightedTotal = 0m;
            decimal weights = 0m;
            for (var index = 0; index < scores.Count; index++)
            {
                var weight = index + 1;
                weightedTotal += scores[index] * weight;
                weights += weight;
            }

            return weights == 0m ? 0m : weightedTotal / weights;
        }

        private static bool IsType(string? actual, string expected) =>
            string.Equals(actual?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        private static bool IsReviewResource(string? type) =>
            IsType(type, "page") || IsType(type, "file") || IsType(type, "link");

        private static bool IsActionable(string? type) =>
            IsReviewResource(type) || IsType(type, "quiz") || IsType(type, "assignment");

        private static int ResourcePriority(string? type) => type?.Trim().ToLowerInvariant() switch
        {
            "page" => 0,
            "file" => 1,
            "link" => 2,
            "quiz" => 3,
            "assignment" => 4,
            _ => 5
        };

        private static bool IsCompleted(TopicProgress? progress) =>
            string.Equals(progress?.Status, "completed", StringComparison.OrdinalIgnoreCase);

        private static bool IsStarted(TopicProgress? progress) =>
            string.Equals(progress?.Status, "in_progress", StringComparison.OrdinalIgnoreCase);

        private static bool IsSameRecommendation(LearningRecommendationDto existing, Topic candidate) =>
            existing.TopicId == candidate.Id
            || (IsType(existing.TopicType, candidate.Type ?? string.Empty)
                && string.Equals(existing.Title.Trim(), (candidate.Title ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

        private sealed record QuizScore(decimal Percent, decimal TotalMark);
        private sealed record WeakTopic(Guid TopicId, string TopicType, decimal ScorePercent, decimal PassThreshold, int AttemptCount, DateTime LastAttemptAt, IReadOnlyList<LearningScoreEvidenceDto> RecentResults);
    }
}
