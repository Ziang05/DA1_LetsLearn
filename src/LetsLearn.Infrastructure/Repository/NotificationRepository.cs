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
    public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
    {
        public NotificationRepository(LetsLearnContext context) : base(context)
        {
        }

        public async Task<List<Notification>> GetByUserIdOrderByCreatedAtDescAsync(
            Guid userId,
            CancellationToken ct = default)
        {
            return await _dbSet
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }
    }
}
