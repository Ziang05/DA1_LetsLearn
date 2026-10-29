using System.Text.Json;

namespace LetsLearn.UseCases.DTOs
{
    public sealed class UpsertAssignmentRubricRequest
    {
        public decimal MaxScore { get; set; } = 10m;
        public string? TeacherGuidance { get; set; }
        public List<UpsertAssignmentRubricCriterionRequest> Criteria { get; set; } = new();
    }

    public sealed class UpsertAssignmentRubricCriterionRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal MaxScore { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class AssignmentRubricResponse
    {
        public Guid Id { get; init; }
        public Guid TopicId { get; init; }
        public int Version { get; init; }
        public decimal MaxScore { get; init; }
        public string? TeacherGuidance { get; init; }
        public DateTime CreatedAt { get; init; }
        public IReadOnlyList<AssignmentRubricCriterionResponse> Criteria { get; init; } = Array.Empty<AssignmentRubricCriterionResponse>();
    }

    public sealed class AssignmentRubricCriterionResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public decimal MaxScore { get; init; }
        public int SortOrder { get; init; }
    }

    public sealed class AiAssignmentGradingJobResponse
    {
        public Guid JobId { get; init; }
        public Guid AssignmentResponseId { get; init; }
        public string Status { get; init; } = string.Empty;
        public int RubricVersion { get; init; }
        public int Attempt { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? StartedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public AiAssignmentGradeDraftResponse? Draft { get; init; }
    }

    public sealed class AiAssignmentGradeDraftResponse
    {
        public Guid DraftId { get; init; }
        public decimal SuggestedScore { get; init; }
        public JsonElement CriterionResults { get; init; }
        public string SummaryFeedback { get; init; } = string.Empty;
        public JsonElement Strengths { get; init; }
        public JsonElement Improvements { get; init; }
        public JsonElement StudySuggestions { get; init; }
        public string ReviewStatus { get; init; } = string.Empty;
        public int ReviewVersion { get; init; }
        public decimal? FinalScore { get; init; }
        public string? FinalFeedback { get; init; }
        public string? ReviewComment { get; init; }
        public Guid? ReviewedBy { get; init; }
        public DateTime? ReviewedAt { get; init; }
    }

    public sealed class AiAssignmentGradingJobAcceptedResponse
    {
        public Guid JobId { get; init; }
        public string Status { get; init; } = string.Empty;
        public string StatusUrl { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }

    public sealed class AssignmentGradingHistoryResponse
    {
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public IReadOnlyList<AiAssignmentGradingJobResponse> Items { get; init; } = Array.Empty<AiAssignmentGradingJobResponse>();
    }

    public sealed class PublishAiAssignmentGradeRequest
    {
        public decimal FinalScore { get; set; }
        public string? FinalFeedback { get; set; }
        public int ExpectedReviewVersion { get; set; }
    }

    public sealed class RejectAiAssignmentGradeRequest
    {
        public string? Reason { get; set; }
        public int ExpectedReviewVersion { get; set; }
    }

    public sealed class PublishedAssignmentGradeResponse
    {
        public Guid AssignmentResponseId { get; init; }
        public decimal Score { get; init; }
        public string? Feedback { get; init; }
        public DateTime? GradedAt { get; init; }
    }

    public sealed class AiAssignmentGradingApiError
    {
        public string Code { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }
}
