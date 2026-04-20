using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Repository;
using LetsLearn.Infrastructure.Data;
using Microsoft.Extensions.Logging;
namespace LetsLearn.Infrastructure.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly LetsLearnContext _context;

        public IUserRepository Users { get; private set; }
        public IRefreshTokenRepository RefreshTokens { get; private set; }
        public UnitOfWork(LetsLearnContext context)
        {
            _context = context;

            Users = new UserRepository(_context);
            RefreshTokens = new RefreshTokenRepository(_context);
        }

        public async Task<int> CommitAsync() =>
            await _context.SaveChangesAsync();

        public async ValueTask DisposeAsync() =>
            await _context.DisposeAsync();
    }
}
