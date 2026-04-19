using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using LetsLearn.Core.Interfaces;

namespace LetsLearn.Infrastructure.Repository
{
    public class GenericRepository<T> : IRepository<T> where T : class
    {
        protected readonly LetsLearnContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(LetsLearnContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<IEnumerable<T?>> GetAllAsync(CancellationToken ct = default) => await _dbSet.AsNoTracking().ToListAsync();

        public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);

        public async Task<IEnumerable<T?>> FindAsync(Expression<Func<T, bool>> expression, CancellationToken ct = default) 
        => await _dbSet.AsNoTracking().Where(expression).ToListAsync();

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        public Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
            return Task.CompletedTask;
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate, ct);
        }
        public async Task<T?> FirstOrDefaultTrackedAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate, ct);
        }

        public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _dbSet.AnyAsync(predicate, ct);
    }
}
