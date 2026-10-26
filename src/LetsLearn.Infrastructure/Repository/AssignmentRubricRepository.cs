using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.Infrastructure.Repository
{
    public class AssignmentRubricRepository : GenericRepository<AssignmentRubric>, IAssignmentRubricRepository
    {
        public AssignmentRubricRepository(LetsLearnContext context) : base(context)
        {
        }

        public async Task<AssignmentRubric?> GetLatestByTopicIdAsync(Guid topicId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Include(rubric => rubric.Criteria)
                .Where(rubric => rubric.TopicId == topicId)
                .OrderByDescending(rubric => rubric.Version)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<AssignmentRubric?> GetByTopicIdAndVersionAsync(
            Guid topicId,
            int version,
            CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Include(rubric => rubric.Criteria)
                .FirstOrDefaultAsync(rubric => rubric.TopicId == topicId && rubric.Version == version, ct);
        }

        public async Task<int> GetNextVersionAsync(Guid topicId, CancellationToken ct = default)
        {
            var latestVersion = await _dbSet
                .Where(rubric => rubric.TopicId == topicId)
                .Select(rubric => (int?)rubric.Version)
                .MaxAsync(ct);

            return (latestVersion ?? 0) + 1;
        }
    }
}
