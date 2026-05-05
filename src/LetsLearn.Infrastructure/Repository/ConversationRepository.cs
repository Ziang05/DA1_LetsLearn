using LetsLearn.Core.Entities;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using LetsLearn.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

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
            // Get DM conversations
            var dmConversations = await _dbSet
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .ToListAsync(ct);

            // Get group conversations (where User2Id is the sentinel: either Guid.Empty or the Id itself)
            var enrolledCourseIds = await _context.Enrollments
                .Where(e => e.StudentId == userId)
                .Select(e => e.CourseId)
                .ToListAsync(ct);

            var groupConversations = await _dbSet
                .Where(c => (c.User2Id == Guid.Empty || c.User2Id == c.Id) && enrolledCourseIds.Contains(c.Id.ToString()))
                .ToListAsync(ct);

            return dmConversations
                .Concat(groupConversations)
                .DistinctBy(c => c.Id)
                .OrderByDescending(c => c.UpdatedAt)
                .ToList();
        }
    }
}
