using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IConversationService
    {
        Task<ConversationDTO> CreateConversationAsync(Guid user1Id, Guid user2Id);
        Task<IEnumerable<ConversationDTO>> GetAllByUserIdAsync(Guid userId);
    }
}
