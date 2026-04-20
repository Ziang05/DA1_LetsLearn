using LetsLearn.Core.Entities;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using LetsLearn.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Infrastructure.Repository
{
    public class ConversationRepository : GenericRepository<Conversation>, IConversationRepository
    {
        public ConversationRepository(LetsLearnContext context) : base(context)
        {
        }

        public async Task<Conversation?> FindByUsersAsync(Guid user1Id, Guid user2Id, CancellationToken ct = default)
        {
            return await _dbSet
                .Where(c => (c.User1Id == user1Id && c.User2Id == user2Id) || (c.User1Id == user2Id && c.User2Id == user1Id))
                .FirstOrDefaultAsync(ct);
        }

        public async Task<List<Conversation>> FindAllByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            return await _dbSet
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync(ct);
        }
    }
}
