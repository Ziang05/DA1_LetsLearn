namespace LetsLearn.UseCases.DTOs
{
    public sealed class LearningRecommendationsResponseDto
    {
        public string CourseId { get; init; } = string.Empty;
        public DateTime GeneratedAtUtc { get; init; }
        public string Summary { get; init; } = string.Empty;
        public LearningAnalysisEvidenceDto Analysis { get; init; } = new();
        public IReadOnlyList<LearningRecommendationDto> Items { get; init; } = Array.Empty<LearningRecommendationDto>();
    }

    public sealed class LearningAnalysisEvidenceDto
    {
        public int TopicsInCourse { get; init; }
        public int CompletedTopics { get; init; }
        public int QuizAttemptsFound { get; init; }
        public int QuizAttemptsReviewed { get; init; }
        public int AssignmentSubmissionsFound { get; init; }
        public int GradedAssignmentsReviewed { get; init; }
        public int WeakTopicsFound { get; init; }
        public string Method { get; init; } = string.Empty;

        // ── Nâng cấp: Thông tin phân tích mở rộng ───────────────────────────
        /// <summary>Điểm sức khỏe học tập tổng thể 0–100.</summary>
        public int OverallHealthScore { get; init; }

        /// <summary>Số topic đang bị stuck (in_progress nhưng không hoạt động lâu).</summary>
        public int StuckTopicsCount { get; init; }

        /// <summary>Số ngày không có hoạt động học tập (-1 nếu chưa bắt đầu).</summary>
        public int InactiveDays { get; init; }

        /// <summary>Các điểm nhấn ngắn gọn về tình trạng học tập.</summary>
        public IReadOnlyList<string> KeyInsights { get; init; } = Array.Empty<string>();

        /// <summary>Sức khỏe học tập theo từng section (chương).</summary>
        public IReadOnlyList<SectionHealthDto> SectionHealthItems { get; init; } = Array.Empty<SectionHealthDto>();
    }

    /// <summary>Điểm sức khỏe học tập của một section (chương).</summary>
    public sealed class SectionHealthDto
    {
        public string SectionId { get; init; } = string.Empty;
        public string SectionTitle { get; init; } = string.Empty;
        public int Position { get; init; }
        public int TotalTopics { get; init; }
        public int CompletedTopics { get; init; }
        public decimal CompletionPercent { get; init; }
        public decimal? AvgQuizScore { get; init; }
        public decimal? AvgAssignmentScore { get; init; }

        /// <summary>
        /// Điểm sức khỏe tổng hợp 0–100 của section.
        /// Kết hợp: 40% completion + 40% avg score + 20% activity recency.
        /// </summary>
        public int HealthScore { get; init; }

        /// <summary>"healthy" | "at_risk" | "critical" | "not_started"</summary>
        public string HealthStatus { get; init; } = "not_started";
    }

    public sealed class LearningScoreEvidenceDto
    {
        public DateTime? RecordedAtUtc { get; init; }
        public decimal ScorePercent { get; init; }
        public decimal? EarnedMark { get; init; }
        public decimal? MaximumMark { get; init; }
    }

    public sealed class LearningRecommendationDto
    {
        public Guid TopicId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string TopicType { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public string EvidenceLabel { get; init; } = string.Empty;
        public decimal? ScorePercent { get; init; }
        public decimal? PassThresholdPercent { get; init; }
        public int? AttemptCount { get; init; }
        public string AnalysisBasis { get; init; } = string.Empty;
        public string? SectionTitle { get; init; }
        public string? ObservedProgressStatus { get; init; }
        public DateTime? LastAccessedAtUtc { get; init; }
        public IReadOnlyList<LearningScoreEvidenceDto> RecentResults { get; init; } = Array.Empty<LearningScoreEvidenceDto>();
        public string Priority { get; init; } = "normal";
        public string ActionLabel { get; init; } = "Open";

        // ── Trend Analysis (Bước 1) ──────────────────────────────────────────
        /// <summary>
        /// Xu hướng điểm qua các lần làm gần nhất:
        /// "improving" | "declining" | "stable" | null (không đủ dữ liệu)
        /// </summary>
        public string? Trend { get; init; }

        /// <summary>
        /// Độ thay đổi điểm từ lần đầu đến lần cuối trong window phân tích (đơn vị %).
        /// Dương = đang tăng, âm = đang giảm.
        /// </summary>
        public decimal? TrendDelta { get; init; }

        // ── Time Decay Factor (Bước 3) ───────────────────────────────────────
        /// <summary>
        /// Số ngày kể từ lần truy cập / làm bài cuối cùng.
        /// </summary>
        public int? DaysSinceLastAccess { get; init; }

        /// <summary>
        /// Hệ số suy giảm do thời gian (0.0 – 1.0). Càng lâu không làm → càng thấp
        /// → điểm thực tế được boost lên để ưu tiên gợi ý lại.
        /// </summary>
        public decimal? DecayFactor { get; init; }

        /// <summary>
        /// Điểm hiệu chỉnh sau khi áp dụng decay: ScorePercent * DecayFactor.
        /// Đây là con số thực sự dùng để ranking recommendation.
        /// </summary>
        public decimal? DecayAdjustedScore { get; init; }

        // ── Stuck Detection ──────────────────────────────────────────────────
        /// <summary>
        /// true nếu topic đang in_progress nhưng không có hoạt động > StuckThresholdDays.
        /// </summary>
        public bool IsStuck { get; init; }
    }
}
