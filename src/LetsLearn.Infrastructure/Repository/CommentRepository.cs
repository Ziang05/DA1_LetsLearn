using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Infrastructure.Repository
{
    public class CommentRepository : GenericRepository<Comment>, ICommentRepository
    {
        public CommentRepository(LetsLearnContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Comment>> FindByTopicIdAsync(Guid topicId, CancellationToken ct = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(c => c.TopicId == topicId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);
        }
    }
}
