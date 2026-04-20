using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Interfaces
{
    public interface ITopicQuizRepository : IRepository<TopicQuiz>
    {
        Task UpdateAsync(TopicQuiz topic);
        Task<TopicQuiz?> GetWithQuestionsAsync(Guid topicId);
        Task<IReadOnlyList<TopicQuiz>> FindByTopicsAndOpenCloseAsync(IReadOnlyList<Guid> topicIds, DateTime start, DateTime end, CancellationToken ct = default);
    }
}
