using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Interfaces
{
    public interface IQuestionRepository : IRepository<Question>
    {
        Task<List<Question>> GetAllByCourseIdAsync(String courseId, CancellationToken ct = default);
        Task<Question?> GetWithChoicesAsync(Guid id, CancellationToken ct = default);
    }
}
