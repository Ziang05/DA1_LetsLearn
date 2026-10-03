using System.Linq.Expressions;
using System.Text.Json;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LetsLearn.Test.Services;

public class AiChatSummaryTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAiLlmClient> _llm = new();
    private readonly Guid _conversationId = Guid.NewGuid();
    private readonly Guid _senderId = Guid.NewGuid();

    private AiChatService CreateService(int messageCount = 2)
    {
        var messages = Enumerable.Range(0, messageCount).Select(i => new Message
        {
            Id = Guid.NewGuid(), ConversationId = _conversationId, SenderId = _senderId,
            Content = $"Message {i}", Timestamp = new DateTime(2026, 1, 1, 0, i, 0, DateTimeKind.Utc),
            FileName = i == 1 ? "assignment.zip" : null, FileUrl = i == 1 ? "https://example.com/file.zip" : null
        }).Reverse().ToList();
        _uow.Setup(u => u.Messages.GetRecentMessagesAsync(_conversationId, 50, It.IsAny<CancellationToken>())).ReturnsAsync(messages);
        _uow.Setup(u => u.Users.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new User { Id = _senderId, Username = "Student" } });
        return new AiChatService(_uow.Object, Mock.Of<IAiQuestionGenerationService>(), _llm.Object, NullLogger<AiChatService>.Instance);
    }

    [Fact]
    public async Task UsesChronologicalMessagesAndAttachmentNames()
    {
        var service = CreateService();
        string? prompt = null;
        _llm.Setup(l => l.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>(), "GeneratorModel"))
            .Callback<string, string, decimal, CancellationToken, string>((_, p, _, _, _) => prompt = p)
            .ReturnsAsync(JsonSerializer.Serialize(new { summary = "## Chủ đề chính\n- Bài tập" }));

        Assert.StartsWith("## Chủ đề chính", await service.SummarizeChatAsync(_conversationId, 50));
        Assert.NotNull(prompt);
        Assert.True(prompt.IndexOf("Message 0", StringComparison.Ordinal) < prompt.IndexOf("Message 1", StringComparison.Ordinal));
        Assert.Contains("assignment.zip", prompt);
        Assert.DoesNotContain("https://example.com/file.zip", prompt);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"summary\":null}")]
    [InlineData("{\"summary\":123}")]
    [InlineData("{\"summary\":\" \"}")]
    public async Task RejectsMalformedSummaryInsteadOfDisplayingRawJson(string response)
    {
        var service = CreateService();
        _llm.Setup(l => l.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>(), "GeneratorModel")).ReturnsAsync(response);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SummarizeChatAsync(_conversationId, 50));
    }

    [Fact]
    public async Task DoesNotCallAiWithoutEnoughMessages()
    {
        var service = CreateService(1);
        Assert.Contains("ít nhất 2", await service.SummarizeChatAsync(_conversationId, 50));
        _llm.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    public async Task RejectsUnboundedMessageLimits(int limit)
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() => service.SummarizeChatAsync(_conversationId, limit));
        _llm.VerifyNoOtherCalls();
    }
}
