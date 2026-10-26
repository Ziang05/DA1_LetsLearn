namespace LetsLearn.Core.Entities
{
    public static class AiAssignmentGradingJobStatuses
    {
        public const string Queued = "queued";
        public const string Processing = "processing";
        public const string Succeeded = "succeeded";
        public const string Failed = "failed";
        public const string Stale = "stale";
    }

    public static class AiAssignmentGradeReviewStatuses
    {
        public const string Pending = "pending";
        public const string Published = "published";
        public const string Rejected = "rejected";
        public const string Stale = "stale";
    }

    public class AssignmentRubric
    {
        public Guid Id { get; set; }
        public Guid TopicId { get; set; }
        public int Version { get; set; }
        public decimal MaxScore { get; set; } = 10m;
        public string? TeacherGuidance { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public ICollection<AssignmentRubricCriterion> Criteria { get; set; } = new List<AssignmentRubricCriterion>();
    }

    public class AssignmentRubricCriterion
    {
        public Guid Id { get; set; }
        public Guid RubricId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal MaxScore { get; set; }
        public int SortOrder { get; set; }
    }

    public class AiAssignmentGradingJob
    {
        public Guid Id { get; set; }
        public Guid AssignmentResponseId { get; set; }
        public Guid RequestedBy { get; set; }
        public Guid RubricId { get; set; }
        public int RubricVersion { get; set; }
        public string SubmissionTextSnapshot { get; set; } = string.Empty;
        public string AssignmentDescriptionSnapshot { get; set; } = string.Empty;
        public string SubmissionHash { get; set; } = string.Empty;
        public string InputHash { get; set; } = string.Empty;
        public string RequestHash { get; set; } = string.Empty;
        public string PromptVersion { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Status { get; set; } = AiAssignmentGradingJobStatuses.Queued;
        public int Attempt { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? LeaseExpiresAt { get; set; }
        public Guid? LeaseToken { get; set; }
        public DateTime AvailableAt { get; set; }
        public AiAssignmentGradeDraft? Draft { get; set; }
    }

    public class AiAssignmentGradeDraft
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public decimal SuggestedScore { get; set; }
        public string CriterionResultsJson { get; set; } = "[]";
        public string SummaryFeedback { get; set; } = string.Empty;
        public string StrengthsJson { get; set; } = "[]";
        public string ImprovementsJson { get; set; } = "[]";
        public string StudySuggestionsJson { get; set; } = "[]";
        public string ReviewStatus { get; set; } = AiAssignmentGradeReviewStatuses.Pending;
        public decimal? FinalScore { get; set; }
        public string? FinalFeedback { get; set; }
        public string? ReviewComment { get; set; }
        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int ReviewVersion { get; set; } = 1;
        public AiAssignmentGradingJob Job { get; set; } = null!;
    }
}
