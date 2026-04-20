using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Interfaces
{
    public interface ICourseRepository : IRepository<Course>
    {
        Task<Course?> GetByIdAsync(string id, CancellationToken ct = default);
        Task<IEnumerable<Course?>> GetAllCoursesByIsPublishedTrue();
        Task <IEnumerable<Course?>> GetByCreatorId(Guid Id, CancellationToken ct = default);
        Task<bool> ExistByTitle(string title);
        Task<List<Course>> GetByIdsAsync(IEnumerable<string> courseIds, CancellationToken ct = default);
    }
}
