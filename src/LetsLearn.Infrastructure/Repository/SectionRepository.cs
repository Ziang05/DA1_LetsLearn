using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LetsLearn.Core.Entities;

namespace LetsLearn.Infrastructure.Repository
{
    public class SectionRepository : GenericRepository<Section>, ISectionRepository
    {
        public SectionRepository(LetsLearnContext context) : base(context) { }

        public async Task<Section?> GetByIdWithTopicsAsync(Guid id, CancellationToken ct = default)
            => await _dbSet.Include(s => s.Topics)
                           .AsNoTracking()
                           .FirstOrDefaultAsync(s => s.Id == id, ct);

        public async Task<Section?> GetTrackedByIdAsync(Guid id, CancellationToken ct = default)
            => await _dbSet.Include(s => s.Topics) 
                           .FirstOrDefaultAsync(s => s.Id == id, ct);
    }
}
