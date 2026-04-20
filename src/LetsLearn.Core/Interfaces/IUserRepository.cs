using System;
using System.Collections.Generic;
using System.Linq;
using LetsLearn.Core.Entities;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Interfaces
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
        Task<List<User>> GetAllUsersWithRolesAsync();
        Task<User?> GetByIdWithEnrollmentsAsync(Guid id, CancellationToken ct = default);

    }
}
