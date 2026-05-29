using System;
using System.Threading;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IAiChatService
    {
        Task<string> SummarizeChatAsync(Guid conversationId, int limit, CancellationToken ct = default);
        Task<Guid> GenerateQuizFromSharedDocumentAsync(Guid messageId, string bloomLevel, int questionCount, Guid userId, CancellationToken ct = default);
    }
}
