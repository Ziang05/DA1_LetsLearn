using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IMessageService
    {
        Task CreateMessageAsync(CreateMessageRequest dto, Guid SenderId);
        Task<IEnumerable<GetMessageResponse>> GetMessagesByConversationIdAsync(Guid conversationId);
        Task<bool> IsUserInConversationAsync(Guid userId, Guid conversationId);
    }
}
