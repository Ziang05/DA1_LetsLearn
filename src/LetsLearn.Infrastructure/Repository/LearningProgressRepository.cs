using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.Infrastructure.Repository
{
    public class LearningProgressRepository : GenericRepository<LearningProgress>, ILearningProgressRepository
    {
        public LearningProgressRepository(LetsLearnContext context) : base(context)
        {
        }

        public async Task<LearningProgress?> GetByStudentAndCourseAsync(Guid studentId, string courseId, CancellationToken ct = default)
        {
            return await _dbSet.FirstOrDefaultAsync(p => p.StudentId == studentId && p.CourseId == courseId, ct);
        }

        public async Task<List<LearningProgress>> GetByCourseAsync(string courseId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(p => p.CourseId == courseId)
                .OrderByDescending(p => p.ProgressPercent)
                .ThenByDescending(p => p.LastActivityAt)
                .ToListAsync(ct);
        }

        public Task UpdateAsync(LearningProgress progress)
        {
            _context.Entry(progress).State = EntityState.Modified;
            return Task.CompletedTask;
        }
    }

    public class TopicProgressRepository : GenericRepository<TopicProgress>, ITopicProgressRepository
    {
        public TopicProgressRepository(LetsLearnContext context) : base(context)
        {
        }

        public async Task<TopicProgress?> GetByStudentAndTopicAsync(Guid studentId, Guid topicId, CancellationToken ct = default)
        {
            return await _dbSet.FirstOrDefaultAsync(p => p.StudentId == studentId && p.TopicId == topicId, ct);
        }

        public async Task<List<TopicProgress>> GetByStudentAndCourseAsync(Guid studentId, string courseId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(p => p.StudentId == studentId && p.CourseId == courseId)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync(ct);
        }

        public async Task<int> CountCompletedByStudentAndCourseAsync(Guid studentId, string courseId, CancellationToken ct = default)
        {
            return await _dbSet.CountAsync(
                p => p.StudentId == studentId
                    && p.CourseId == courseId
                    && p.Status == "completed",
                ct);
        }

        public Task UpdateAsync(TopicProgress progress)
        {
            _context.Entry(progress).State = EntityState.Modified;
            return Task.CompletedTask;
        }
    }
}
