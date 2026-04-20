using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Interfaces
{
    public interface IConversationRepository : IRepository<Conversation>
    {
        Task<Conversation?> FindByUsersAsync(Guid user1Id, Guid user2Id, CancellationToken ct = default);
        Task<List<Conversation>> FindAllByUserIdAsync(Guid userId, CancellationToken ct = default);
    }
}
