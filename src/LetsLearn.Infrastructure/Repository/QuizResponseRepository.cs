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
    public class QuizResponseRepository : GenericRepository<QuizResponse>, IQuizResponseRepository
    {
        public QuizResponseRepository(LetsLearnContext context) : base(context)
        {
        }

        public async Task<List<QuizResponse>> FindAllByTopicIdAsync(Guid topicId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(qr => qr.TopicId == topicId)
                .Include(qr => qr.Answers)
                .ToListAsync(ct);
        }

        public async Task<List<QuizResponse>> FindAllByStudentIdAsync(Guid studentId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(qr => qr.StudentId == studentId)
                .ToListAsync(ct);
        }

        public async Task<List<QuizResponse>> FindByTopicIdAndStudentIdAsync(Guid topicId, Guid studentId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(qr => qr.TopicId == topicId && qr.StudentId == studentId)
                .ToListAsync(ct);
        }

        public async Task<List<QuizResponse>> FindByTopicIdsAndStudentIdAsync(List<Guid> topicIds, Guid studentId, CancellationToken ct = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Include(qr => qr.Answers)
                .Where(qr => topicIds.Contains(qr.TopicId) && qr.StudentId == studentId)
                .ToListAsync(ct);
        }

        public async Task<QuizResponse?> GetByIdTrackedWithAnswersAsync(Guid id, CancellationToken ct)
        {
            return await _dbSet
                .Include(q => q.Answers)
                .FirstOrDefaultAsync(q => q.Id == id);
        }
        public Task<List<QuizResponse>> FindAllByTopicIdWithAnswersAsync(Guid topicId, CancellationToken ct)
        {
            return _context.QuizResponses
                .Where(q => q.TopicId == topicId)
                .Include(q => q.Answers)
                .ToListAsync(ct);
        }

        public Task<List<QuizResponse>> FindByTopicIdAndStudentIdWithAnswersAsync(Guid topicId, Guid studentId, CancellationToken ct)
        {
            return _context.QuizResponses
                .Where(q => q.TopicId == topicId && q.StudentId == studentId)
                .Include(q => q.Answers)
                .ToListAsync(ct);
        }
    }
}
