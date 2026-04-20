using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Infrastructure.Repository
{
    public class QuestionRepository : GenericRepository<Question>, IQuestionRepository
    {
        private readonly ILogger<QuestionRepository> _logger;

        public QuestionRepository(LetsLearnContext context, ILogger<QuestionRepository> logger) : base(context)
        {
            _logger = logger;
        }

        public async Task<List<Question>> GetAllByCourseIdAsync(String courseId, CancellationToken ct = default)
        {
            _logger.LogDebug("Querying questions for course {CourseId}", courseId);

            var query = _dbSet
                .Include(q => q.Choices)
                .Where(q => q.CourseId == courseId && q.DeletedAt == null);

            // Log the generated SQL for debugging
            _logger.LogDebug("SQL Query for GetAllByCourseIdAsync: {Query}", query.ToQueryString());

            var questions = await query.ToListAsync(ct);

            _logger.LogInformation("Found {QuestionCount} non-deleted questions for course {CourseId}",
                questions.Count, courseId);

            // Log all questions in the database for this course (including deleted ones) for debugging
            var allQuestions = await _dbSet
                .Where(q => q.CourseId == courseId)
                .Select(q => new { q.Id, q.QuestionName, q.DeletedAt, q.CourseId })
                .ToListAsync(ct);

            _logger.LogDebug("All questions in database for course {CourseId}: {AllQuestions}",
                courseId,
                string.Join(", ", allQuestions.Select(q => $"ID:{q.Id}, Name:{q.QuestionName}, Deleted:{q.DeletedAt}")));

            return questions;
        }

        public async Task<Question?> GetWithChoicesAsync(Guid id, CancellationToken ct = default)
        {
            return await _dbSet
                .Include(q => q.Choices)
                .FirstOrDefaultAsync(q => q.Id == id && q.DeletedAt == null, ct);
        }
    }
}
