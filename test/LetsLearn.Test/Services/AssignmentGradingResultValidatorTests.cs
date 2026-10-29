using System.Text.Json;
using LetsLearn.Core.Entities;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.Services.AiAssignmentGrading;

namespace LetsLearn.Test.Services;

public sealed class AssignmentGradingResultValidatorTests
{
    private readonly AssignmentGradingResultValidator _validator = new();
    private readonly Guid _firstCriterionId = Guid.NewGuid();
    private readonly Guid _secondCriterionId = Guid.NewGuid();
    private const string Submission = "The answer explains photosynthesis in plants.";

    [Fact]
    public void AcceptsValidScoresAndVerbatimEvidence()
    {
        var result = _validator.Validate(CreateJson(
            ( _firstCriterionId, 3m, "Clear explanation", new[] { ("photosynthesis in plants", (int?)1) }),
            (_secondCriterionId, 5m, "Good detail", Array.Empty<(string, int?)>())), Submission, CreateRubric());

        Assert.Equal(8m, result.SuggestedScore);
        Assert.Contains("criterionId", result.CriterionResultsJson);
        Assert.Equal("Overall feedback", result.SummaryFeedback);
    }

    [Theory]
    [InlineData("not-json", "invalid_result_json")]
    [InlineData("{}", "invalid_result_schema")]
    public void RejectsMalformedProviderJson(string json, string expectedCode)
    {
        var error = Assert.Throws<AssignmentGradingException>(() => _validator.Validate(json, Submission, CreateRubric()));
        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public void RejectsMissingCriterion()
    {
        var json = CreateJson((_firstCriterionId, 3m, "Clear explanation", Array.Empty<(string, int?)>()));
        AssertCode("criterion_set_mismatch", json);
    }

    [Fact]
    public void RejectsCriterionScoreAboveRubricMaximum()
    {
        var json = CreateJson(
            (_firstCriterionId, 4.01m, "Too high", Array.Empty<(string, int?)>()),
            (_secondCriterionId, 5m, "Good detail", Array.Empty<(string, int?)>()));
        AssertCode("criterion_score_out_of_range", json);
    }

    [Fact]
    public void RejectsEvidenceThatDoesNotAppearInSubmission()
    {
        var json = CreateJson(
            (_firstCriterionId, 3m, "Clear explanation", new[] { ("fabricated evidence", (int?)null) }),
            (_secondCriterionId, 5m, "Good detail", Array.Empty<(string, int?)>()));
        AssertCode("evidence_not_in_submission", json);
    }

    [Fact]
    public void RejectsDuplicateCriterionIds()
    {
        var json = CreateJson(
            (_firstCriterionId, 3m, "First", Array.Empty<(string, int?)>()),
            (_firstCriterionId, 5m, "Repeated", Array.Empty<(string, int?)>()));
        AssertCode("duplicate_criterion", json);
    }

    [Fact]
    public void RejectsRubricWhoseMaximumDoesNotEqualTen()
    {
        var rubric = CreateRubric();
        rubric.MaxScore = 9m;
        rubric.Criteria.Last().MaxScore = 5m;
        AssertCode("invalid_rubric", CreateJson(
            (_firstCriterionId, 3m, "First", Array.Empty<(string, int?)>()),
            (_secondCriterionId, 5m, "Second", Array.Empty<(string, int?)>())), rubric: rubric);
    }

    private void AssertCode(string expectedCode, string json, AssignmentRubric? rubric = null)
    {
        var error = Assert.Throws<AssignmentGradingException>(() => _validator.Validate(json, Submission, rubric ?? CreateRubric()));
        Assert.Equal(expectedCode, error.Code);
    }

    private AssignmentRubric CreateRubric() => new()
    {
        Id = Guid.NewGuid(),
        MaxScore = 10m,
        Criteria = new List<AssignmentRubricCriterion>
        {
            new() { Id = _firstCriterionId, Name = "Accuracy", Description = "Correctness", MaxScore = 4m, SortOrder = 0 },
            new() { Id = _secondCriterionId, Name = "Reasoning", Description = "Explanation", MaxScore = 6m, SortOrder = 1 }
        }
    };

    private static string CreateJson(params (Guid Id, decimal Score, string Feedback, (string Quote, int? Page)[] Evidence)[] criteria) =>
        JsonSerializer.Serialize(new
        {
            criterionResults = criteria.Select(item => new
            {
                criterionId = item.Id,
                score = item.Score,
                feedback = item.Feedback,
                evidence = item.Evidence.Select(evidence => new { quote = evidence.Quote, page = evidence.Page })
            }),
            summaryFeedback = "Overall feedback",
            strengths = new[] { "Accurate terminology" },
            improvements = new[] { "Explain one more step" },
            studySuggestions = new[] { "Review the lesson" }
        });
}
