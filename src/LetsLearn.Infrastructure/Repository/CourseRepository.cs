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
    public class CourseRepository : GenericRepository<Course>, ICourseRepository
    {
        public CourseRepository(LetsLearnContext context) : base(context)
        {
        }
        public async Task<Course?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            return await _dbSet.Include(c => c.Sections)
                               .ThenInclude(s => s.Topics)
                               .FirstOrDefaultAsync(c => c.Id == id, ct);
        }

        public async Task<bool> ExistByTitle(string title)
        {
            return await ExistsAsync(c => c.Title == title);
        }

        public async Task<IEnumerable<Course?>> GetAllCoursesByIsPublishedTrue()
        {
            return await _dbSet.Include(c => c.Sections)
                               .ThenInclude(s => s.Topics)
                               .Where(c => c.IsPublished == true)
                               .ToListAsync();
        }

        public async Task<IEnumerable<Course?>> GetByCreatorId(Guid id, CancellationToken ct = default)
        {
            return await _dbSet.Include(c => c.Sections)
                               .ThenInclude(s => s.Topics)
                               .Where(c => c.CreatorId == id)
                               .ToListAsync(ct);
        }

        public async Task<List<Course>> GetByIdsAsync(IEnumerable<string> courseIds, CancellationToken ct = default)
        {
            return await _context.Courses
                                 .Include(c => c.Sections)
                                 .ThenInclude(s => s.Topics)
                                 .Where(c => courseIds.Contains(c.Id))
                                 .ToListAsync(ct);
        }
    }
}
