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
    public class EnrollmentRepository : GenericRepository<Enrollment>, IEnrollmentRepository
    {
        public EnrollmentRepository(LetsLearnContext context) : base(context)
        {
        }

        public async Task<Enrollment?> GetByIdsAsync(Guid studentId, string courseId, CancellationToken ct = default)
        {
            return await _dbSet
                .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == courseId, ct);
        }

        public async Task<List<Enrollment>> GetAllByStudentIdAsync(Guid studentId, CancellationToken ct = default)
        {
            return await _dbSet
                .Where(e => e.StudentId == studentId)
                .ToListAsync(ct);
        }

        public async Task<List<Enrollment>> GetAllByCourseIdAsync(string courseId, CancellationToken ct = default)
        {
            return await _dbSet
                .Where(e => e.CourseId == courseId)
                .ToListAsync(ct);
        }

        public async Task<int> CountByCourseIdAndJoinDateLessThanEqualAsync(string courseId, DateTime date, CancellationToken ct = default)
        {
            return await _dbSet.CountAsync(e => e.CourseId == courseId && e.JoinDate <= date, ct);
        }

        public async Task<List<Enrollment>> GetByCourseIdAndJoinDateLessThanEqualAsync(string courseId, DateTime date, CancellationToken ct = default)
        {
            return await _dbSet
                .Where(e => e.CourseId == courseId && e.JoinDate <= date)
                .ToListAsync(ct);
        }

        public async Task<List<Enrollment>> GetByStudentIdAndJoinDateLessThanEqualAsync(Guid studentId, DateTime date, CancellationToken ct = default)
        {
            return await _dbSet
                .Where(e => e.StudentId == studentId && e.JoinDate <= date)
                .ToListAsync(ct);
        }

        public async Task DeleteByStudentIdAndCourseIdAsync(Guid studentId, string courseId, CancellationToken ct = default)
        {
            var enrollment = await GetByIdsAsync(studentId, courseId, ct);
            if (enrollment != null)
            {
                _dbSet.Remove(enrollment);
            }
        }

        public async Task<List<Enrollment>> GetByStudentId(Guid studentId, CancellationToken ct = default)
        {
            return await _context.Enrollments
                                 .Where(e => e.StudentId == studentId)
                                 .ToListAsync(ct);
        }
        public async Task<int> GetStudentCountByRoleAsync(string courseId, CancellationToken ct = default)
        {
            return await _context.Enrollments
                .Where(e => e.CourseId == courseId)
                .Join(_context.Users,
                      e => e.StudentId,
                      u => u.Id,
                      (e, u) => u)
                .CountAsync(u => u.Role.ToLower() == "student" || u.Role.ToLower() == "learner", ct);
        }

        public async Task<Dictionary<string, int>> GetStudentCountsByRoleAsync(IEnumerable<string> courseIds, CancellationToken ct = default)
        {
            return await _context.Enrollments
                .Where(e => courseIds.Contains(e.CourseId))
                .Join(_context.Users,
                      e => e.StudentId,
                      u => u.Id,
                      (e, u) => new { e.CourseId, u.Role })
                .Where(x => x.Role.ToLower() == "student" || x.Role.ToLower() == "learner")
                .GroupBy(x => x.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CourseId, x => x.Count, ct);
        }
    }
}
