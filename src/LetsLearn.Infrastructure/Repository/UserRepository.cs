using LetsLearn.Infrastructure.Data;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Infrastructure.Repository
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(LetsLearnContext context) : base(context) { }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            return await _dbSet.Include(u => u.Enrollments)
                               .FirstOrDefaultAsync(u => u.Email == email, ct);
        }

        public async Task<List<User>> GetAllUsersWithRolesAsync()
        {
            return await _dbSet.AsNoTracking()
                               .Include(u => u.Role)
                               .Include(u => u.Enrollments)
                               .ToListAsync();
        }

        public async Task<User?> GetByIdWithEnrollmentsAsync(Guid id, CancellationToken ct = default)
        {
            return await _dbSet
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == id, ct);
        }
    }
}
