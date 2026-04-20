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
    public class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
    {
        public RefreshTokenRepository(LetsLearnContext context) : base(context) { }

        public async Task<RefreshToken?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            return await _dbSet.FirstOrDefaultAsync(rt => rt.UserId == userId, ct);
        }

        public async Task AddOrUpdateAsync(RefreshToken token, CancellationToken ct = default)
        {
            var existing = await GetByUserIdAsync(token.UserId, ct);
            if (existing != null)
            {
                existing.Token = token.Token;
                existing.ExpiryDate = token.ExpiryDate;
                _dbSet.Update(existing);
            }
            else
            {
                await AddAsync(token);
            }
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token) 
        {
            return await _dbSet.FirstOrDefaultAsync(r => r.Token == token);
        }
    }
}
