using LetsLearn.Core.Entities;

namespace LetsLearn.Core.Interfaces
{
    public interface IAssignmentRubricRepository : IRepository<AssignmentRubric>
    {
        Task<AssignmentRubric?> GetLatestByTopicIdAsync(Guid topicId, CancellationToken ct = default);
        Task<AssignmentRubric?> GetByTopicIdAndVersionAsync(Guid topicId, int version, CancellationToken ct = default);
        Task<int> GetNextVersionAsync(Guid topicId, CancellationToken ct = default);
    }
}
