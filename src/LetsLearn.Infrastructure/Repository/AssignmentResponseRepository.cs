using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Infrastructure.Repository
{
    public class AssignmentResponseRepository : GenericRepository<AssignmentResponse>, IAssignmentResponseRepository
    {
        public AssignmentResponseRepository(LetsLearnContext context) : base(context)
        {
        }

        public async Task<IEnumerable<AssignmentResponse>> GetAllByTopicIdAsync(Guid topicId)
        {
            return await _dbSet.AsNoTracking().Include(ar => ar.Files).Where(ar => ar.TopicId == topicId).ToListAsync();
        }

        public async Task<IEnumerable<AssignmentResponse>> GetAllByStudentIdAsync(Guid studentId)
        {
            return await _dbSet.AsNoTracking().Where(ar => ar.StudentId == studentId).ToListAsync();
        }

        public async Task<AssignmentResponse?> GetByTopicIdAndStudentIdAsync(Guid topicId, Guid studentId)
        {
            return await _dbSet.AsNoTracking().FirstOrDefaultAsync(ar => ar.TopicId == topicId && ar.StudentId == studentId);
        }

        public async Task<AssignmentResponse?> GetByTopicIdAndStudentIdWithFilesAsync(Guid topicId, Guid studentId)
        {
            return await _dbSet.AsNoTracking().Include(ar => ar.Files).FirstOrDefaultAsync(ar => ar.TopicId == topicId && ar.StudentId == studentId);
        }

        public async Task<IEnumerable<AssignmentResponse>> GetByTopicIdsAndStudentIdAsync(IEnumerable<Guid> topicIds, Guid studentId)
        {
            return await _dbSet.AsNoTracking().Where(ar => topicIds.Contains(ar.TopicId) && ar.StudentId == studentId).ToListAsync();
        }

        public async Task<AssignmentResponse?> GetByIdWithFilesAsync(Guid id)
        {
            return await _dbSet.AsNoTracking().Include(ar => ar.Files).FirstOrDefaultAsync(ar => ar.Id == id);
        }

        public async Task<IEnumerable<AssignmentResponse>> GetAllByTopicIdWithFilesAsync(Guid topicId)
        {
            return await _dbSet.AsNoTracking().Include(ar => ar.Files).Where(ar => ar.TopicId == topicId).ToListAsync();
        }

        public async Task<AssignmentResponse?> GetByIdTrackedWithFilesAsync(Guid id)
        {
            return await _dbSet.Include(ar => ar.Files).FirstOrDefaultAsync(ar => ar.Id == id);
        }

        public async Task<IReadOnlyList<AssignmentResponse>> FindByTopicIdsAndStudentIdAsync(
            IEnumerable<Guid> topicIds,
            Guid studentId,
            CancellationToken ct = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Include(ar => ar.Files)
                .Where(ar => topicIds.Contains(ar.TopicId) && ar.StudentId == studentId)
                .ToListAsync(ct);
        }

    }
}
