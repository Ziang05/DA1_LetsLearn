using System.Text.Json;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;

namespace LetsLearn.UseCases.Services
{
    public sealed class LearningRecommendationService : ILearningRecommendationService
    {
        // ── Constants ─────────────────────────────────────────────────────────
        private const decimal DefaultWeakScoreThreshold = 0.6m;

        /// <summary>Topic bị coi là "stuck" nếu in_progress và không hoạt động quá số ngày này.</summary>
        private const int StuckThresholdDays = 7;

        /// <summary>
        /// Thời gian half-life cho time decay (ngày).
        /// Sau N ngày không làm → memory retention giảm ~50% theo mô hình Ebbinghaus.
        /// </summary>
        private const double DecayHalfLifeDays = 21.0;

        /// <summary>Ngưỡng trend: thay đổi điểm > N% thì mới coi là improving/declining.</summary>
        private const decimal TrendSignificanceThreshold = 5m;

        private readonly IUnitOfWork _unitOfWork;

        public LearningRecommendationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ═════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═════════════════════════════════════════════════════════════════════

        public async Task<LearningRecommendationsResponseDto> GetMyRecommendationsAsync(
            string courseId,
            Guid studentId,
            int limit = 3,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(courseId))
                throw new ArgumentException("CourseId is required.", nameof(courseId));

            if (limit < 1 || limit > 10)
                throw new ArgumentException("Limit must be between 1 and 10.", nameof(limit));

            _ = await _unitOfWork.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found.");
            _ = await _unitOfWork.Enrollments.GetByIdsAsync(studentId, courseId, ct)
                ?? throw new UnauthorizedAccessException("Student is not enrolled in this course.");

            // ── 1. Load dữ liệu cơ bản ───────────────────────────────────────
            var sections = (await _unitOfWork.Sections.FindAsync(s => s.CourseId == courseId, ct))
                .Where(s => s != null).Cast<Section>()
                .OrderBy(s => s.Position ?? int.MaxValue).ThenBy(s => s.Title)
                .ToList();

            var sectionIds = sections.Select(s => s.Id).ToList();
            var topics = sectionIds.Count == 0
                ? new List<Topic>()
                : (await _unitOfWork.Topics.GetAllBySectionIdsAsync(sectionIds, ct)).ToList();
            var topicIds = topics.Select(t => t.Id).ToList();

            var sectionById = sections.ToDictionary(s => s.Id);
            var topicById   = topics.ToDictionary(t => t.Id);

            var progressByTopicId = (await _unitOfWork.TopicProgresses.GetByStudentAndCourseAsync(studentId, courseId, ct))
                .ToDictionary(p => p.TopicId);

            // ── 2. Load quiz data ─────────────────────────────────────────────
            var quizTopicIds = topics.Where(t => IsType(t.Type, "quiz")).Select(t => t.Id).ToList();

            var quizzes = quizTopicIds.Count == 0
                ? new Dictionary<Guid, TopicQuiz>()
                : (await _unitOfWork.TopicQuizzes.FindAsync(q => quizTopicIds.Contains(q.TopicId), ct))
                    .Where(q => q != null).Cast<TopicQuiz>()
                    .ToDictionary(q => q.TopicId);

            var quizAttempts = topicIds.Count == 0
                ? new List<QuizResponse>()
                : await _unitOfWork.QuizResponses.FindByTopicIdsAndStudentIdAsync(topicIds, studentId, ct);

            var validQuizAttempts = quizAttempts
                .Where(a => quizTopicIds.Contains(a.TopicId) && topicById.ContainsKey(a.TopicId))
                .Select(a => new { Attempt = a, Score = CalculateTrustedQuizScore(a) })
                .Where(x => x.Score != null)
                .ToList();

            // ── 3. Phân tích quiz với Trend + Decay ───────────────────────────
            var scoredQuizTopics = validQuizAttempts
                .GroupBy(x => x.Attempt.TopicId)
                .Select(group =>
                {
                    var attempts = group
                        .OrderBy(x => x.Attempt.CompletedAt)
                        .TakeLast(3).ToList();

                    var scores       = attempts.Select(x => x.Score!.Percent).ToList();
                    var weightedScore = WeightedRecentScore(scores);
                    var lastAttemptAt = group.Max(x => x.Attempt.CompletedAt) ?? DateTime.MinValue;
                    var latest       = attempts[^1].Score!;

                    var passThreshold = GetPassThreshold(quizzes.GetValueOrDefault(group.Key), latest.TotalMark);

                    var trend      = DetectTrend(scores);
                    var trendDelta = scores.Count >= 2 ? scores[^1] - scores[0] : (decimal?)null;

                    var decayFactor = CalculateDecayFactor(lastAttemptAt);
                    var daysSince   = (int)(DateTime.UtcNow - lastAttemptAt).TotalDays;

                    // Điểm hiệu chỉnh: nếu trend đang giảm → thêm penalty, nếu tăng → giảm urgency nhẹ
                    var trendMultiplier = trend == "declining" ? 0.85m : (trend == "improving" ? 1.05m : 1.0m);
                    var decayAdjustedScore = Math.Clamp(weightedScore * (decimal)decayFactor * trendMultiplier, 0m, 100m);

                    var recentResults = attempts.Select(x =>
                    {
                        var s = x.Score!;
                        return new LearningScoreEvidenceDto
                        {
                            RecordedAtUtc = x.Attempt.CompletedAt,
                            ScorePercent  = Math.Round(s.Percent, 1),
                            EarnedMark    = Math.Round(s.Percent * s.TotalMark / 100m, 2),
                            MaximumMark   = Math.Round(s.TotalMark, 2)
                        };
                    }).ToList();

                    return new EnrichedWeakTopic(
                        TopicId:            group.Key,
                        TopicType:          "quiz",
                        WeightedScore:      weightedScore,
                        PassThreshold:      passThreshold,
                        AttemptCount:       attempts.Count,
                        LastAttemptAt:      lastAttemptAt,
                        RecentResults:      recentResults,
                        Trend:              trend,
                        TrendDelta:         trendDelta,
                        DecayFactor:        (decimal)decayFactor,
                        DaysSinceLastAccess: daysSince,
                        DecayAdjustedScore: decayAdjustedScore);
                })
                .Where(r => r.DecayAdjustedScore < r.PassThreshold * 100m)
                // Rank theo decay-adjusted score: càng thấp càng ưu tiên
                .OrderBy(r => r.DecayAdjustedScore / Math.Max(r.PassThreshold, 0.01m))
                .ThenByDescending(r => r.LastAttemptAt)
                .ToList();

            // ── 4. Phân tích assignment với Decay ─────────────────────────────
            var assignmentResponses = topicIds.Count == 0
                ? Array.Empty<AssignmentResponse>()
                : (await _unitOfWork.AssignmentResponses.FindByTopicIdsAndStudentIdAsync(topicIds, studentId, ct)).ToArray();

            var assignmentTopicIds = assignmentResponses.Select(r => r.TopicId).Distinct().ToList();
            var rubrics = assignmentTopicIds.Count == 0
                ? new List<AssignmentRubric>()
                : (await _unitOfWork.AssignmentRubrics.FindAsync(r => assignmentTopicIds.Contains(r.TopicId), ct))
                    .Where(r => r != null).Cast<AssignmentRubric>().ToList();

            var analyzedAssignmentTopics = assignmentResponses
                .Where(r => r.Mark.HasValue && r.GradedAt.HasValue
                    && topicById.TryGetValue(r.TopicId, out var t) && IsType(t.Type, "assignment"))
                .Select(r =>
                {
                    var rubric = rubrics
                        .Where(rb => rb.TopicId == r.TopicId && rb.CreatedAt <= r.GradedAt!.Value)
                        .OrderByDescending(rb => rb.Version).FirstOrDefault();
                    if (rubric == null || rubric.MaxScore <= 0m) return null;

                    return new
                    {
                        Response     = r,
                        ScorePercent = Math.Clamp(r.Mark!.Value / rubric.MaxScore, 0m, 1m) * 100m,
                        EarnedMark   = r.Mark.Value,
                        MaximumMark  = rubric.MaxScore
                    };
                })
                .Where(x => x != null && topicById.ContainsKey(x.Response.TopicId))
                .GroupBy(x => x!.Response.TopicId)
                .Select(group =>
                {
                    var attempts = group
                        .OrderBy(x => x!.Response.GradedAt)
                        .TakeLast(3).ToList();

                    var scores        = attempts.Select(x => x!.ScorePercent).ToList();
                    var weightedScore  = WeightedRecentScore(scores);
                    var lastGradedAt  = attempts[^1]!.Response.GradedAt!.Value;
                    var trend         = DetectTrend(scores);
                    var trendDelta    = scores.Count >= 2 ? scores[^1] - scores[0] : (decimal?)null;
                    var decayFactor   = CalculateDecayFactor(lastGradedAt);
                    var daysSince     = (int)(DateTime.UtcNow - lastGradedAt).TotalDays;
                    var trendMultiplier = trend == "declining" ? 0.85m : (trend == "improving" ? 1.05m : 1.0m);
                    var decayAdjustedScore = Math.Clamp(weightedScore * (decimal)decayFactor * trendMultiplier, 0m, 100m);

                    var recentResults = attempts.Select(x => new LearningScoreEvidenceDto
                    {
                        RecordedAtUtc = x!.Response.GradedAt,
                        ScorePercent  = Math.Round(x.ScorePercent, 1),
                        EarnedMark    = Math.Round(x.EarnedMark, 2),
                        MaximumMark   = Math.Round(x.MaximumMark, 2)
                    }).ToList();

                    return new EnrichedWeakTopic(
                        TopicId:            group.Key,
                        TopicType:          "assignment",
                        WeightedScore:      weightedScore,
                        PassThreshold:      DefaultWeakScoreThreshold,
                        AttemptCount:       attempts.Count,
                        LastAttemptAt:      lastGradedAt,
                        RecentResults:      recentResults,
                        Trend:              trend,
                        TrendDelta:         trendDelta,
                        DecayFactor:        (decimal)decayFactor,
                        DaysSinceLastAccess: daysSince,
                        DecayAdjustedScore: decayAdjustedScore);
                })
                .ToList();

            var scoredAssignmentTopics = analyzedAssignmentTopics
                .Where(r => r.DecayAdjustedScore < r.PassThreshold * 100m)
                .ToList();

            // ── 5. Merge + Unified ranking bằng decay-adjusted score ──────────
            var weakTopics = scoredQuizTopics
                .Concat(scoredAssignmentTopics)
                .OrderBy(r => r.DecayAdjustedScore / Math.Max(r.PassThreshold, 0.01m))
                .ThenByDescending(r => r.LastAttemptAt)
                .ToList();

            // ── 6. Stuck topic detection ──────────────────────────────────────
            var stuckTopics = progressByTopicId.Values
                .Where(p => IsStarted(p)
                    && p.LastAccessedAt.HasValue
                    && (DateTime.UtcNow - p.LastAccessedAt.Value).TotalDays > StuckThresholdDays)
                .ToList();

            // ── 7. Section Health Score ───────────────────────────────────────
            var sectionHealthItems = BuildSectionHealth(
                sections, topics, progressByTopicId,
                scoredQuizTopics, scoredAssignmentTopics);

            // ── 8. Tính OverallHealthScore ────────────────────────────────────
            var overallHealth = CalculateOverallHealth(
                topics.Count,
                progressByTopicId.Values.Count(IsCompleted),
                weakTopics.Count,
                stuckTopics.Count,
                sectionHealthItems);

            // ── 9. Tính InactiveDays ──────────────────────────────────────────
            var lastActivityAt = progressByTopicId.Values
                .Select(p => p.LastAccessedAt)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .DefaultIfEmpty()
                .Max();
            var inactiveDays = lastActivityAt == default
                ? -1
                : (int)(DateTime.UtcNow - lastActivityAt).TotalDays;

            // ── 10. Build Key Insights ────────────────────────────────────────
            var keyInsights = BuildKeyInsights(
                weakTopics, stuckTopics, inactiveDays, sectionHealthItems,
                progressByTopicId.Values.Count(IsCompleted), topics.Count);

            // ── 11. Build recommendations ─────────────────────────────────────
            var recommendations = new List<LearningRecommendationDto>();

            // 11a. Stuck topics → ưu tiên đầu tiên (bị bỏ dở lâu)
            foreach (var progress in stuckTopics.OrderBy(p => p.LastAccessedAt).Take(1))
            {
                if (!topicById.TryGetValue(progress.TopicId, out var stuckTopic)) continue;
                if (recommendations.Any(r => IsSameRecommendation(r, stuckTopic))) continue;

                var daysSinceStuck = (int)(DateTime.UtcNow - (progress.LastAccessedAt ?? DateTime.UtcNow)).TotalDays;
                recommendations.Add(new LearningRecommendationDto
                {
                    TopicId                = stuckTopic.Id,
                    Title                  = stuckTopic.Title ?? "Nội dung học",
                    TopicType              = stuckTopic.Type ?? "page",
                    Reason                 = $"Bạn đã bắt đầu nội dung này nhưng chưa hoàn thành từ {daysSinceStuck} ngày trước. Hãy tiếp tục để không bị đứt mạch học.",
                    EvidenceLabel          = $"Đang dở · {daysSinceStuck} ngày chưa tiếp tục",
                    AnalysisBasis          = $"Hệ thống phát hiện bạn đã mở nội dung này {daysSinceStuck} ngày trước nhưng chưa đánh dấu hoàn thành. Kiến thức dễ bị quên khi bị gián đoạn.",
                    SectionTitle           = sectionById.GetValueOrDefault(stuckTopic.SectionId)?.Title,
                    ObservedProgressStatus = "in_progress",
                    LastAccessedAtUtc      = progress.LastAccessedAt,
                    Priority               = daysSinceStuck > 14 ? "high" : "normal",
                    ActionLabel            = "Học tiếp",
                    IsStuck                = true,
                    DaysSinceLastAccess    = daysSinceStuck
                });

                if (recommendations.Count >= limit) break;
            }

            // 11b. Weak topics (điểm thấp, ưu tiên theo decay-adjusted score)
            foreach (var weakTopic in weakTopics)
            {
                if (recommendations.Count >= limit) break;
                if (!topicById.TryGetValue(weakTopic.TopicId, out var topic)) continue;
                if (recommendations.Any(r => IsSameRecommendation(r, topic))) continue;

                var trendText = weakTopic.Trend switch
                {
                    "declining"  => $" Đáng lo ngại: điểm đang có xu hướng giảm ({weakTopic.TrendDelta:+0.#;-0.#;0}%).",
                    "improving"  => $" Tốt: điểm đang tăng ({weakTopic.TrendDelta:+0.#}%), hãy tiếp tục cải thiện.",
                    _            => string.Empty
                };

                var decayNote = weakTopic.DaysSinceLastAccess > 14
                    ? $" Đã {weakTopic.DaysSinceLastAccess} ngày không ôn — kiến thức có thể đã phai nhạt."
                    : string.Empty;

                var reason = weakTopic.TopicType == "quiz"
                    ? $"Điểm tổng hợp (có tính suy giảm theo thời gian) là {Math.Round(weakTopic.DecayAdjustedScore)}%, thấp hơn mốc {Math.Round(weakTopic.PassThreshold * 100m)}%.{trendText}{decayNote}"
                    : $"Điểm bài tập hiệu chỉnh là {Math.Round(weakTopic.DecayAdjustedScore)}%, dưới mức gợi ý 60%.{trendText}{decayNote}";

                var analysisBasis = weakTopic.TopicType == "quiz"
                    ? $"Hệ thống xem tối đa 3 lần làm gần nhất, chú trọng kết quả mới nhất, sau đó áp dụng hệ số suy giảm theo thời gian (mô hình Ebbinghaus, half-life {DecayHalfLifeDays} ngày). Trend: {TrendLabel(weakTopic.Trend)}."
                    : $"Hệ thống xem tối đa 3 bài đã chấm, áp dụng hệ số thời gian tương tự. Trend: {TrendLabel(weakTopic.Trend)}.";

                var isHighPriority = weakTopic.Trend == "declining"
                    || weakTopic.DecayAdjustedScore < weakTopic.PassThreshold * 100m * 0.75m
                    || weakTopic.DaysSinceLastAccess > 21;

                recommendations.Add(new LearningRecommendationDto
                {
                    TopicId               = topic.Id,
                    Title                 = topic.Title ?? (weakTopic.TopicType == "quiz" ? "Quiz" : "Bài tập"),
                    TopicType             = topic.Type ?? weakTopic.TopicType,
                    Reason                = reason,
                    EvidenceLabel         = weakTopic.TopicType == "quiz"
                        ? $"{Math.Round(weakTopic.WeightedScore)}% → {Math.Round(weakTopic.DecayAdjustedScore)}% (sau decay) · {weakTopic.AttemptCount} lần"
                        : $"{Math.Round(weakTopic.WeightedScore)}% → {Math.Round(weakTopic.DecayAdjustedScore)}% (sau decay) · đã chấm",
                    ScorePercent          = Math.Round(weakTopic.WeightedScore, 1),
                    PassThresholdPercent  = Math.Round(weakTopic.PassThreshold * 100m, 1),
                    AttemptCount          = weakTopic.AttemptCount,
                    AnalysisBasis         = analysisBasis,
                    SectionTitle          = sectionById.GetValueOrDefault(topic.SectionId)?.Title,
                    ObservedProgressStatus = progressByTopicId.GetValueOrDefault(topic.Id)?.Status,
                    LastAccessedAtUtc     = progressByTopicId.GetValueOrDefault(topic.Id)?.LastAccessedAt,
                    RecentResults         = weakTopic.RecentResults,
                    Priority              = isHighPriority ? "high" : "normal",
                    ActionLabel           = weakTopic.TopicType == "quiz" ? "Ôn và làm lại" : "Xem nhận xét",
                    Trend                 = weakTopic.Trend,
                    TrendDelta            = weakTopic.TrendDelta.HasValue ? Math.Round(weakTopic.TrendDelta.Value, 1) : null,
                    DaysSinceLastAccess   = weakTopic.DaysSinceLastAccess,
                    DecayFactor           = Math.Round(weakTopic.DecayFactor, 3),
                    DecayAdjustedScore    = Math.Round(weakTopic.DecayAdjustedScore, 1),
                    IsStuck               = false
                });

                // 11c. Gợi ý tài liệu cùng section (khi quiz yếu)
                if (weakTopic.TopicType == "quiz" && recommendations.Count < limit)
                {
                    var sectionResource = topics
                        .Where(c => c.SectionId == topic.SectionId
                            && IsReviewResource(c.Type)
                            && !IsCompleted(progressByTopicId.GetValueOrDefault(c.Id))
                            && recommendations.All(r => r.TopicId != c.Id))
                        .OrderBy(c => ResourcePriority(c.Type))
                        .ThenBy(c => c.Title)
                        .FirstOrDefault();

                    if (sectionResource != null)
                    {
                        recommendations.Add(new LearningRecommendationDto
                        {
                            TopicId                = sectionResource.Id,
                            Title                  = sectionResource.Title ?? "Tài liệu ôn tập",
                            TopicType              = sectionResource.Type ?? "page",
                            Reason                 = "Tài liệu chưa hoàn thành trong cùng phần có thể giúp bạn ôn lại trước khi làm lại bài kiểm tra.",
                            EvidenceLabel          = "Tài liệu cùng phần",
                            AnalysisBasis          = "Nội dung này nằm cùng section với bài kiểm tra cần ôn và bạn chưa hoàn thành.",
                            SectionTitle           = sectionById.GetValueOrDefault(sectionResource.SectionId)?.Title,
                            ObservedProgressStatus = progressByTopicId.GetValueOrDefault(sectionResource.Id)?.Status,
                            LastAccessedAtUtc      = progressByTopicId.GetValueOrDefault(sectionResource.Id)?.LastAccessedAt,
                            Priority               = "normal",
                            ActionLabel            = "Mở tài liệu"
                        });
                    }
                }
            }

            // 11d. Fallback: nội dung chưa học / đang dở
            if (recommendations.Count == 0)
            {
                var nextTopics = topics
                    .Where(t => IsActionable(t.Type) && !IsCompleted(progressByTopicId.GetValueOrDefault(t.Id)))
                    .OrderBy(t => sectionById.GetValueOrDefault(t.SectionId)?.Position ?? int.MaxValue)
                    .ThenBy(t => IsStarted(progressByTopicId.GetValueOrDefault(t.Id)) ? 0 : 1)
                    .ThenBy(t => IsStarted(progressByTopicId.GetValueOrDefault(t.Id))
                        ? progressByTopicId.GetValueOrDefault(t.Id)?.LastAccessedAt ?? DateTime.MaxValue
                        : DateTime.MaxValue)
                    .ThenBy(t => ResourcePriority(t.Type))
                    .ThenBy(t => t.Title)
                    .Take(limit).ToList();

                foreach (var topic in nextTopics)
                {
                    if (recommendations.Any(r => IsSameRecommendation(r, topic))) continue;
                    var inProgress = IsStarted(progressByTopicId.GetValueOrDefault(topic.Id));
                    recommendations.Add(new LearningRecommendationDto
                    {
                        TopicId                = topic.Id,
                        Title                  = topic.Title ?? "Nội dung khóa học",
                        TopicType              = topic.Type ?? "page",
                        Reason                 = inProgress
                            ? "Bạn đã bắt đầu nội dung này nhưng chưa đánh dấu hoàn thành."
                            : "Nội dung này chưa được bắt đầu trong khóa học.",
                        EvidenceLabel          = inProgress ? "Đang học" : "Chưa bắt đầu",
                        AnalysisBasis          = inProgress
                            ? "Bạn đã mở nội dung này nhưng chưa hoàn thành."
                            : "Bạn chưa bắt đầu nội dung này; đây là bước tiếp theo trong lộ trình học.",
                        SectionTitle           = sectionById.GetValueOrDefault(topic.SectionId)?.Title,
                        ObservedProgressStatus = progressByTopicId.GetValueOrDefault(topic.Id)?.Status ?? "not_started",
                        LastAccessedAtUtc      = progressByTopicId.GetValueOrDefault(topic.Id)?.LastAccessedAt,
                        Priority               = inProgress ? "high" : "normal",
                        ActionLabel            = inProgress ? "Học tiếp" : "Bắt đầu học"
                    });
                }
            }

            // ── 12. Build Summary ─────────────────────────────────────────────
            var summary = BuildSummary(recommendations, weakTopics, stuckTopics, inactiveDays, topics.Count, overallHealth);

            return new LearningRecommendationsResponseDto
            {
                CourseId       = courseId,
                GeneratedAtUtc = DateTime.UtcNow,
                Summary        = summary,
                Analysis       = new LearningAnalysisEvidenceDto
                {
                    TopicsInCourse              = topics.Count,
                    CompletedTopics             = progressByTopicId.Values.Count(IsCompleted),
                    QuizAttemptsFound           = quizAttempts.Count,
                    QuizAttemptsReviewed        = validQuizAttempts.Count,
                    AssignmentSubmissionsFound  = assignmentResponses.Length,
                    GradedAssignmentsReviewed   = analyzedAssignmentTopics.Sum(r => r.AttemptCount),
                    WeakTopicsFound             = weakTopics.Count,
                    OverallHealthScore          = overallHealth,
                    StuckTopicsCount            = stuckTopics.Count,
                    InactiveDays                = inactiveDays,
                    KeyInsights                 = keyInsights,
                    SectionHealthItems          = sectionHealthItems,
                    Method = $"Với bài kiểm tra, hệ thống xem tối đa 3 lần làm gần nhất và chú trọng kết quả mới nhất (weighted), sau đó áp dụng hệ số suy giảm theo thời gian (Ebbinghaus half-life {DecayHalfLifeDays} ngày) và điều chỉnh theo xu hướng điểm (trend). Với bài tập, quy trình tương tự trên các bài đã được chấm. Điểm hiệu chỉnh cuối cùng (decay-adjusted) được dùng để xếp hạng ưu tiên gợi ý."
                },
                Items = recommendations.Take(limit).ToList()
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        // SECTION HEALTH SCORE (Bước 2)
        // ═════════════════════════════════════════════════════════════════════

        private static IReadOnlyList<SectionHealthDto> BuildSectionHealth(
            IReadOnlyList<Section> sections,
            IReadOnlyList<Topic> topics,
            IReadOnlyDictionary<Guid, TopicProgress> progressByTopicId,
            IReadOnlyList<EnrichedWeakTopic> quizResults,
            IReadOnlyList<EnrichedWeakTopic> assignmentResults)
        {
            var quizScoreByTopic       = quizResults.ToDictionary(r => r.TopicId, r => r.WeightedScore);
            var assignmentScoreByTopic = assignmentResults.ToDictionary(r => r.TopicId, r => r.WeightedScore);

            return sections.Select(section =>
            {
                var sectionTopics = topics.Where(t => t.SectionId == section.Id).ToList();
                var totalTopics     = sectionTopics.Count;
                var completedTopics = sectionTopics.Count(t => IsCompleted(progressByTopicId.GetValueOrDefault(t.Id)));

                var completionPct = totalTopics == 0 ? 0m : (decimal)completedTopics / totalTopics * 100m;

                // Điểm quiz trung bình trong section
                var quizScores = sectionTopics
                    .Where(t => IsType(t.Type, "quiz") && quizScoreByTopic.ContainsKey(t.Id))
                    .Select(t => quizScoreByTopic[t.Id]).ToList();
                var avgQuizScore = quizScores.Count > 0 ? quizScores.Average() : (decimal?)null;

                // Điểm assignment trung bình trong section
                var assignmentScores = sectionTopics
                    .Where(t => IsType(t.Type, "assignment") && assignmentScoreByTopic.ContainsKey(t.Id))
                    .Select(t => assignmentScoreByTopic[t.Id]).ToList();
                var avgAssignmentScore = assignmentScores.Count > 0 ? assignmentScores.Average() : (decimal?)null;

                if (totalTopics == 0)
                {
                    return new SectionHealthDto
                    {
                        SectionId    = section.Id.ToString(),
                        SectionTitle = section.Title ?? string.Empty,
                        Position     = section.Position ?? 0,
                        HealthScore  = 0,
                        HealthStatus = "not_started"
                    };
                }

                // ── Health score formula ──────────────────────────────────────
                // 40% completion + 40% avg score (quiz > assignment) + 20% activity recency
                var completionScore = completionPct; // 0–100

                var scoreComponent = avgQuizScore ?? avgAssignmentScore;
                var scoreScore     = scoreComponent ?? (completionPct > 0 ? 70m : 0m); // 0–100

                // Activity recency: xét lastAccessedAt của topic mới nhất trong section
                var lastActivity = sectionTopics
                    .Select(t => progressByTopicId.GetValueOrDefault(t.Id)?.LastAccessedAt)
                    .Where(d => d.HasValue).Select(d => d!.Value)
                    .DefaultIfEmpty().Max();
                var recencyScore = lastActivity == default ? 0m
                    : Math.Max(0m, 100m - (decimal)(DateTime.UtcNow - lastActivity).TotalDays * 2m); // mỗi ngày -2 điểm

                var healthScore = (int)Math.Round(completionScore * 0.4m + scoreScore * 0.4m + recencyScore * 0.2m);
                healthScore = Math.Clamp(healthScore, 0, 100);

                var healthStatus = (completedTopics == 0 && lastActivity == default) ? "not_started"
                    : healthScore >= 75 ? "healthy"
                    : healthScore >= 45 ? "at_risk"
                    : "critical";

                return new SectionHealthDto
                {
                    SectionId          = section.Id.ToString(),
                    SectionTitle       = section.Title ?? string.Empty,
                    Position           = section.Position ?? 0,
                    TotalTopics        = totalTopics,
                    CompletedTopics    = completedTopics,
                    CompletionPercent  = Math.Round(completionPct, 1),
                    AvgQuizScore       = avgQuizScore.HasValue ? Math.Round(avgQuizScore.Value, 1) : null,
                    AvgAssignmentScore = avgAssignmentScore.HasValue ? Math.Round(avgAssignmentScore.Value, 1) : null,
                    HealthScore        = healthScore,
                    HealthStatus       = healthStatus
                };
            })
            .OrderBy(s => s.Position)
            .ToList();
        }

        // ═════════════════════════════════════════════════════════════════════
        // OVERALL HEALTH SCORE
        // ═════════════════════════════════════════════════════════════════════

        private static int CalculateOverallHealth(
            int totalTopics,
            int completedTopics,
            int weakCount,
            int stuckCount,
            IReadOnlyList<SectionHealthDto> sectionHealth)
        {
            if (totalTopics == 0) return 0;

            // Base: completion %
            var completionScore = (decimal)completedTopics / totalTopics * 100m;

            // Section health average (loại bỏ "not_started")
            var activeSections = sectionHealth.Where(s => s.HealthStatus != "not_started").ToList();
            var sectionAvg = activeSections.Count > 0
                ? activeSections.Average(s => (decimal)s.HealthScore)
                : completionScore;

            // Penalty: mỗi weak topic -3 điểm, mỗi stuck topic -5 điểm
            var penalty = weakCount * 3m + stuckCount * 5m;

            var health = (int)Math.Round((completionScore * 0.3m + sectionAvg * 0.7m) - penalty);
            return Math.Clamp(health, 0, 100);
        }

        // ═════════════════════════════════════════════════════════════════════
        // KEY INSIGHTS BUILDER
        // ═════════════════════════════════════════════════════════════════════

        private static IReadOnlyList<string> BuildKeyInsights(
            IReadOnlyList<EnrichedWeakTopic> weakTopics,
            IReadOnlyList<TopicProgress> stuckTopics,
            int inactiveDays,
            IReadOnlyList<SectionHealthDto> sectionHealth,
            int completedTopics,
            int totalTopics)
        {
            var insights = new List<string>();

            // Inactivity warning
            if (inactiveDays >= 7)
                insights.Add($"⚠️ Bạn chưa học trong {inactiveDays} ngày — kiến thức có thể đang bị quên theo thời gian.");
            else if (inactiveDays >= 3)
                insights.Add($"💡 Bạn chưa học trong {inactiveDays} ngày. Nên học đều đặn hơn để giữ kiến thức.");

            // Stuck topics
            if (stuckTopics.Count > 0)
                insights.Add($"🔄 Có {stuckTopics.Count} nội dung đang bị bỏ dở lâu. Hãy hoàn thành để tránh bị đứt mạch.");

            // Declining trends
            var decliningCount = weakTopics.Count(t => t.Trend == "declining");
            if (decliningCount > 0)
                insights.Add($"📉 {decliningCount} nội dung có điểm đang giảm dần qua các lần làm — cần ôn gấp.");

            // Improving trends
            var improvingCount = weakTopics.Count(t => t.Trend == "improving");
            if (improvingCount > 0)
                insights.Add($"📈 {improvingCount} nội dung đang có xu hướng cải thiện — hãy tiếp tục duy trì!");

            // Critical sections
            var criticalSections = sectionHealth.Where(s => s.HealthStatus == "critical").ToList();
            if (criticalSections.Count > 0)
                insights.Add($"🔴 {criticalSections.Count} chương ở mức nguy hiểm: {string.Join(", ", criticalSections.Select(s => $"\"{s.SectionTitle}\""))}.");

            // Completion milestone
            if (totalTopics > 0)
            {
                var pct = (int)((decimal)completedTopics / totalTopics * 100);
                if (pct >= 100) insights.Add("🏆 Bạn đã hoàn thành toàn bộ khóa học!");
                else if (pct >= 75) insights.Add($"🎯 Bạn đã hoàn thành {pct}% khóa học — sắp về đích rồi!");
                else if (pct >= 50) insights.Add($"💪 Đã qua nửa chặng đường ({pct}%)!");
            }

            return insights;
        }

        // ═════════════════════════════════════════════════════════════════════
        // SUMMARY BUILDER
        // ═════════════════════════════════════════════════════════════════════

        private static string BuildSummary(
            IReadOnlyList<LearningRecommendationDto> recommendations,
            IReadOnlyList<EnrichedWeakTopic> weakTopics,
            IReadOnlyList<TopicProgress> stuckTopics,
            int inactiveDays,
            int totalTopics,
            int overallHealth)
        {
            if (recommendations.Count == 0)
            {
                return totalTopics == 0
                    ? "Khóa học chưa có nội dung để đề xuất."
                    : "Bạn đã hoàn thành các nội dung hiện có; không có điểm nào cần ưu tiên ôn tập lúc này.";
            }

            var parts = new List<string>();

            if (weakTopics.Any(t => t.Trend == "declining"))
                parts.Add("một số nội dung đang có điểm giảm dần");
            if (stuckTopics.Count > 0)
                parts.Add($"{stuckTopics.Count} nội dung bị bỏ dở");
            if (inactiveDays >= 7)
                parts.Add($"không hoạt động {inactiveDays} ngày");

            var healthLabel = overallHealth >= 75 ? "Tốt" : overallHealth >= 45 ? "Cần chú ý" : "Cần cải thiện";

            return parts.Count > 0
                ? $"Hệ thống phát hiện {string.Join(", ", parts)}. Sức khỏe học tập tổng thể: {healthLabel} ({overallHealth}/100). Dưới đây là các nội dung được ưu tiên gợi ý."
                : $"Dựa trên tiến độ và kết quả học tập (có tính suy giảm theo thời gian). Sức khỏe học tập: {healthLabel} ({overallHealth}/100).";
        }

        // ═════════════════════════════════════════════════════════════════════
        // TREND ANALYSIS (Bước 1)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Phân tích xu hướng điểm từ danh sách điểm theo thứ tự thời gian.
        /// Dùng linear regression đơn giản: so sánh điểm đầu – cuối với noise filter.
        /// </summary>
        private static string? DetectTrend(IReadOnlyList<decimal> scores)
        {
            if (scores.Count < 2) return null; // Không đủ dữ liệu

            // Linear regression slope ước tính (normalized)
            var n    = scores.Count;
            var xMean = (n - 1) / 2.0m;
            var yMean = scores.Average();

            decimal numerator   = 0m;
            decimal denominator = 0m;

            for (var i = 0; i < n; i++)
            {
                var dx = i - xMean;
                numerator   += dx * (scores[i] - yMean);
                denominator += dx * dx;
            }

            if (denominator == 0m) return "stable";

            var slope = numerator / denominator; // Điểm/lần → slope > 0 = tăng

            return slope > TrendSignificanceThreshold / n ? "improving"
                : slope < -TrendSignificanceThreshold / n ? "declining"
                : "stable";
        }

        // ═════════════════════════════════════════════════════════════════════
        // TIME DECAY FACTOR (Bước 3 — Ebbinghaus Forgetting Curve)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tính hệ số suy giảm theo mô hình Ebbinghaus (exponential decay).
        /// R(t) = e^(-t / half-life * ln2)
        /// t = số ngày kể từ lần làm cuối.
        /// Kết quả: 1.0 (vừa làm) → tiệm cận 0 (lâu không làm).
        /// </summary>
        private static double CalculateDecayFactor(DateTime lastAttemptAt)
        {
            var daysSince = Math.Max(0.0, (DateTime.UtcNow - lastAttemptAt).TotalDays);
            var decay     = Math.Exp(-daysSince * Math.Log(2.0) / DecayHalfLifeDays);
            // Floor 0.3 để tránh penalty quá lớn làm mất đi ý nghĩa của điểm thực tế
            return Math.Max(0.3, decay);
        }

        // ═════════════════════════════════════════════════════════════════════
        // QUIZ SCORE CALCULATOR
        // ═════════════════════════════════════════════════════════════════════

        private static QuizScore? CalculateTrustedQuizScore(QuizResponse attempt)
        {
            if (!string.Equals(attempt.Status, "Finished", StringComparison.OrdinalIgnoreCase)
                || !attempt.CompletedAt.HasValue
                || attempt.Answers.Count == 0)
                return null;

            decimal earned = 0m;
            decimal total  = 0m;

            foreach (var answer in attempt.Answers)
            {
                if (string.IsNullOrWhiteSpace(answer.Question)) return null;

                Question? question;
                try { question = JsonSerializer.Deserialize<Question>(answer.Question); }
                catch (JsonException) { return null; }

                if (question?.DefaultMark is not decimal maxMark || maxMark <= 0m) return null;

                var answerMark = QuizAnswerScorer.CalculateMark(question, answer.Answer);
                if (answerMark is null) return null;

                total  += maxMark;
                earned += Math.Clamp(answerMark.Value, 0m, maxMark);
            }

            return total <= 0m ? null
                : new QuizScore(Math.Clamp(earned / total, 0m, 1m) * 100m, total);
        }

        // ═════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private static decimal GetPassThreshold(TopicQuiz? quiz, decimal totalMark)
        {
            if (quiz?.GradeToPass is decimal passMark && passMark > 0m && totalMark > 0m && passMark <= totalMark)
                return Math.Clamp(passMark / totalMark, 0.01m, 1m);
            return DefaultWeakScoreThreshold;
        }

        private static decimal WeightedRecentScore(IReadOnlyList<decimal> scores)
        {
            decimal weightedTotal = 0m;
            decimal weights       = 0m;
            for (var i = 0; i < scores.Count; i++)
            {
                var w = i + 1;
                weightedTotal += scores[i] * w;
                weights       += w;
            }
            return weights == 0m ? 0m : weightedTotal / weights;
        }

        private static string TrendLabel(string? trend) => trend switch
        {
            "improving" => "đang cải thiện 📈",
            "declining" => "đang giảm 📉",
            "stable"    => "ổn định ➡️",
            _           => "chưa xác định"
        };

        private static bool IsType(string? actual, string expected) =>
            string.Equals(actual?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        private static bool IsReviewResource(string? type) =>
            IsType(type, "page") || IsType(type, "file") || IsType(type, "link");

        private static bool IsActionable(string? type) =>
            IsReviewResource(type) || IsType(type, "quiz") || IsType(type, "assignment");

        private static int ResourcePriority(string? type) => type?.Trim().ToLowerInvariant() switch
        {
            "page"       => 0,
            "file"       => 1,
            "link"       => 2,
            "quiz"       => 3,
            "assignment" => 4,
            _            => 5
        };

        private static bool IsCompleted(TopicProgress? progress) =>
            string.Equals(progress?.Status, "completed", StringComparison.OrdinalIgnoreCase);

        private static bool IsStarted(TopicProgress? progress) =>
            string.Equals(progress?.Status, "in_progress", StringComparison.OrdinalIgnoreCase);

        private static bool IsSameRecommendation(LearningRecommendationDto existing, Topic candidate) =>
            existing.TopicId == candidate.Id
            || (IsType(existing.TopicType, candidate.Type ?? string.Empty)
                && string.Equals(existing.Title.Trim(), (candidate.Title ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase));

        // ═════════════════════════════════════════════════════════════════════
        // PRIVATE RECORDS
        // ═════════════════════════════════════════════════════════════════════

        private sealed record QuizScore(decimal Percent, decimal TotalMark);

        /// <summary>
        /// Phiên bản nâng cấp của WeakTopic với đầy đủ thông tin Trend + Decay.
        /// </summary>
        private sealed record EnrichedWeakTopic(
            Guid TopicId,
            string TopicType,
            decimal WeightedScore,
            decimal PassThreshold,
            int AttemptCount,
            DateTime LastAttemptAt,
            IReadOnlyList<LearningScoreEvidenceDto> RecentResults,
            string? Trend,
            decimal? TrendDelta,
            decimal DecayFactor,
            int DaysSinceLastAccess,
            decimal DecayAdjustedScore);
    }
}
