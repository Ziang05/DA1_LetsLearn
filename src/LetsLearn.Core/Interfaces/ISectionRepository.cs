using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LetsLearn.Core.Entities;

namespace LetsLearn.Core.Interfaces
{
    public interface ISectionRepository : IRepository<Section>
    {
        Task<Section?> GetByIdWithTopicsAsync(Guid id, CancellationToken ct = default);
        Task<Section?> GetTrackedByIdAsync(Guid id, CancellationToken ct = default);
    }
}
