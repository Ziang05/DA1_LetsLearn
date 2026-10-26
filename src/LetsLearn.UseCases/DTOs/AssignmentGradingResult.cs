using System.Text.Json.Serialization;
using LetsLearn.Core.Entities;

namespace LetsLearn.UseCases.DTOs
{
    public sealed class AiAssignmentGradingJobRequestResult
    {
        public Guid JobId { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public bool ReusedExistingJob { get; init; }
    }

    public sealed class AssignmentGradingPrompt
    {
        public string SystemPrompt { get; init; } = string.Empty;
        public string UserPrompt { get; init; } = string.Empty;
    }

    public sealed class AssignmentGradingLlmResult
    {
        [JsonPropertyName("criterionResults")]
        public List<AssignmentCriterionGradeResult>? CriterionResults { get; init; }
        [JsonPropertyName("summaryFeedback")]
        public string? SummaryFeedback { get; init; }
        [JsonPropertyName("strengths")]
        public List<string>? Strengths { get; init; }
        [JsonPropertyName("improvements")]
        public List<string>? Improvements { get; init; }
        [JsonPropertyName("studySuggestions")]
        public List<string>? StudySuggestions { get; init; }
    }

    public sealed class AssignmentCriterionGradeResult
    {
        [JsonPropertyName("criterionId")]
        public Guid CriterionId { get; init; }
        [JsonPropertyName("score")]
        public decimal? Score { get; init; }
        [JsonPropertyName("feedback")]
        public string? Feedback { get; init; }
        [JsonPropertyName("evidence")]
        public List<AssignmentGradeEvidence>? Evidence { get; init; }
    }

    public sealed class AssignmentGradeEvidence
    {
        [JsonPropertyName("quote")]
        public string? Quote { get; init; }
        [JsonPropertyName("page")]
        public int? Page { get; init; }
    }

    public sealed class ValidatedAssignmentGrade
    {
        public decimal SuggestedScore { get; init; }
        public string CriterionResultsJson { get; init; } = "[]";
        public string SummaryFeedback { get; init; } = string.Empty;
        public string StrengthsJson { get; init; } = "[]";
        public string ImprovementsJson { get; init; } = "[]";
        public string StudySuggestionsJson { get; init; } = "[]";

        public AiAssignmentGradeDraft ToDraft(Guid jobId) => new()
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            SuggestedScore = SuggestedScore,
            CriterionResultsJson = CriterionResultsJson,
            SummaryFeedback = SummaryFeedback,
            StrengthsJson = StrengthsJson,
            ImprovementsJson = ImprovementsJson,
            StudySuggestionsJson = StudySuggestionsJson,
            ReviewStatus = AiAssignmentGradeReviewStatuses.Pending,
            ReviewVersion = 1
        };
    }

    public sealed class AssignmentGradingException : Exception
    {
        public AssignmentGradingException(string code, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
