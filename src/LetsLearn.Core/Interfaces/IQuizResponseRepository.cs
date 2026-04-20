using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Interfaces
{
    public interface IQuizResponseRepository : IRepository<QuizResponse>
    {
        Task<List<QuizResponse>> FindAllByTopicIdAsync(Guid topicId, CancellationToken ct = default);
        Task<List<QuizResponse>> FindAllByStudentIdAsync(Guid studentId, CancellationToken ct = default);
        Task<List<QuizResponse>> FindByTopicIdAndStudentIdAsync(Guid topicId, Guid studentId, CancellationToken ct = default);
        Task<List<QuizResponse>> FindByTopicIdsAndStudentIdAsync(List<Guid> topicIds, Guid studentId, CancellationToken ct = default);
        Task<QuizResponse?> GetByIdTrackedWithAnswersAsync(Guid id, CancellationToken ct = default);
        Task<List<QuizResponse>> FindAllByTopicIdWithAnswersAsync(Guid topicId, CancellationToken ct = default);
        Task<List<QuizResponse>> FindByTopicIdAndStudentIdWithAnswersAsync(Guid topicId, Guid studentId, CancellationToken ct = default);
    }
}
