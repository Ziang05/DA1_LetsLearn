using LetsLearn.Core.Entities;

namespace LetsLearn.Core.Interfaces
{
    public interface ILearningProgressRepository : IRepository<LearningProgress>
    {
        Task<LearningProgress?> GetByStudentAndCourseAsync(Guid studentId, string courseId, CancellationToken ct = default);
        Task<List<LearningProgress>> GetByCourseAsync(string courseId, CancellationToken ct = default);
        Task UpdateAsync(LearningProgress progress);
    }

    public interface ITopicProgressRepository : IRepository<TopicProgress>
    {
        Task<TopicProgress?> GetByStudentAndTopicAsync(Guid studentId, Guid topicId, CancellationToken ct = default);
        Task<List<TopicProgress>> GetByStudentAndCourseAsync(Guid studentId, string courseId, CancellationToken ct = default);
        Task<int> CountCompletedByStudentAndCourseAsync(Guid studentId, string courseId, CancellationToken ct = default);
        Task UpdateAsync(TopicProgress progress);
    }
}
