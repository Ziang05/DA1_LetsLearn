using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Interfaces
{
    public interface IEnrollmentRepository : IRepository<Enrollment>
    {
        Task<Enrollment?> GetByIdsAsync(Guid studentId, String courseId, CancellationToken ct = default);

        Task<List<Enrollment>> GetAllByStudentIdAsync(Guid studentId, CancellationToken ct = default);

        Task<List<Enrollment>> GetAllByCourseIdAsync(String courseId, CancellationToken ct = default);

        Task<int> CountByCourseIdAndJoinDateLessThanEqualAsync(string courseId, DateTime date, CancellationToken ct = default);
       
        Task<List<Enrollment>> GetByCourseIdAndJoinDateLessThanEqualAsync(string courseId, DateTime date, CancellationToken ct = default);
        
        Task<List<Enrollment>> GetByStudentIdAndJoinDateLessThanEqualAsync(Guid studentId, DateTime date, CancellationToken ct = default);

        Task DeleteByStudentIdAndCourseIdAsync(Guid studentId, String courseId, CancellationToken ct = default);

        Task<List<Enrollment>> GetByStudentId(Guid studentId, CancellationToken ct = default);
    }
}
