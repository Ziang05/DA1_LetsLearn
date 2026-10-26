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
    }
}
