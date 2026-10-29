using LetsLearn.Core.Entities;

namespace LetsLearn.Core.Interfaces
{
    public interface IAiAssignmentGradeDraftRepository : IRepository<AiAssignmentGradeDraft>
    {
        Task<AiAssignmentGradeDraft?> GetTrackedByIdAsync(Guid draftId, CancellationToken ct = default);
        Task<AiAssignmentGradeDraft?> GetByJobIdAsync(Guid jobId, CancellationToken ct = default);
    }
}
