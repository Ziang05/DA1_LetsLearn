using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Interfaces
{
    public interface INotificationRepository : IRepository<Notification>
    {
        Task<List<Notification>> GetByUserIdOrderByCreatedAtDescAsync(
            Guid userId,
            CancellationToken ct = default);
    }
}
