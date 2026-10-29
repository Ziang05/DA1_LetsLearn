using System.Text.Json;
using LetsLearn.Core.Entities;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.Services.AiAssignmentGrading;
using Microsoft.Extensions.Configuration;
using Moq;

namespace LetsLearn.Test.Services;

public sealed class AssignmentGradingServiceTests
{
    [Fact]
    public async Task ConvertsProviderTimeoutToRetryableGradingError()
    {
        var (service, llm) = CreateService();
        llm.Setup(c => c.GenerateJsonWithOptionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>(),
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>()))
            .ThrowsAsync(new OperationCanceledException("provider timeout"));

        var error = await Assert.ThrowsAsync<AssignmentGradingException>(() => service.GradeAsync(CreateInput(), CreateRubric()));

        Assert.Equal("provider_timeout", error.Code);
    }

    [Fact]
    public async Task ConvertsProvider429ToRateLimitError()
    {
        var (service, llm) = CreateService();
        llm.Setup(c => c.GenerateJsonWithOptionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>(),
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Provider returned 429"));

        var error = await Assert.ThrowsAsync<AssignmentGradingException>(() => service.GradeAsync(CreateInput(), CreateRubric()));

        Assert.Equal("provider_rate_limited", error.Code);
    }

    [Fact]
    public async Task ConvertsMissingProviderConfigurationToSafeError()
    {
        var (service, llm) = CreateService();
        llm.Setup(c => c.GenerateJsonWithOptionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>(),
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var error = await Assert.ThrowsAsync<AssignmentGradingException>(() => service.GradeAsync(CreateInput(), CreateRubric()));

        Assert.Equal("provider_not_configured", error.Code);
    }

    [Fact]
    public async Task ValidatesProviderOutputBeforeReturningDraftData()
    {
        var (service, llm) = CreateService();
        llm.Setup(c => c.GenerateJsonWithOptionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>(),
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>()))
            .ReturnsAsync(JsonSerializer.Serialize(new
            {
                criterionResults = new[] { new { criterionId = _criterionId, score = 8m, feedback = "Correct", evidence = Array.Empty<object>() } },
                summaryFeedback = "Good work",
                strengths = new[] { "Accurate" },
                improvements = Array.Empty<string>(),
                studySuggestions = Array.Empty<string>()
            }));

        var result = await service.GradeAsync(CreateInput(), CreateRubric());

        Assert.Equal(8m, result.SuggestedScore);
        Assert.Equal("Good work", result.SummaryFeedback);
    }

    private static readonly Guid _rubricId = Guid.NewGuid();
    private static readonly Guid _criterionId = Guid.NewGuid();

    private static (AssignmentGradingService Service, Mock<IAiLlmClient> Client) CreateService()
    {
        var llm = new Mock<IAiLlmClient>();
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet(c => c["AIGrading:PromptVersion"]).Returns("grading-v1");
        var service = new AssignmentGradingService(llm.Object, new AssignmentGradingPromptBuilder(), new AssignmentGradingResultValidator(), configuration.Object);
        return (service, llm);
    }

    private static AssignmentRubric CreateRubric() => new()
    {
        Id = _rubricId,
        Version = 1,
        MaxScore = 10m,
        Criteria = new List<AssignmentRubricCriterion>
        {
            new() { Id = _criterionId, Name = "Accuracy", Description = "Correct answer", MaxScore = 10m, SortOrder = 0 }
        }
    };

    private static AssignmentGradingInput CreateInput()
    {
        var rubric = CreateRubric();
        return new AssignmentGradingInput
        {
            AssignmentResponseId = Guid.NewGuid(),
            SubmissionText = "answer text",
            AssignmentDescription = "Answer the question.",
            RubricId = rubric.Id,
            RubricVersion = rubric.Version,
            RubricJson = AssignmentGradingInputHasher.SerializeRubric(rubric),
            PromptVersion = "grading-v1",
            Model = "test-model"
        };
    }
}
