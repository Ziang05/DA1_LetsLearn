using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.Infrastructure.Repository
{
    public class AiAssignmentGradeDraftRepository : GenericRepository<AiAssignmentGradeDraft>, IAiAssignmentGradeDraftRepository
    {
        public AiAssignmentGradeDraftRepository(LetsLearnContext context) : base(context)
        {
        }

        public Task<AiAssignmentGradeDraft?> GetTrackedByIdAsync(Guid draftId, CancellationToken ct = default)
        {
            return _dbSet.FirstOrDefaultAsync(draft => draft.Id == draftId, ct);
        }

        public Task<AiAssignmentGradeDraft?> GetByJobIdAsync(Guid jobId, CancellationToken ct = default)
        {
            return _dbSet.AsNoTracking().FirstOrDefaultAsync(draft => draft.JobId == jobId, ct);
        }
    }
}
