using LetsLearn.UseCases.DTOs;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface ILearningProgressService
    {
        Task<LearningProgressSummaryDto> GetMyCourseProgressAsync(string courseId, Guid studentId, CancellationToken ct = default);
        Task<List<TopicProgressDto>> GetMyTopicProgressesAsync(string courseId, Guid studentId, CancellationToken ct = default);
        Task<TopicProgressDto> MarkTopicViewedAsync(Guid topicId, Guid studentId, CancellationToken ct = default);
        Task<TopicProgressDto> MarkTopicCompletedAsync(Guid topicId, Guid studentId, string completionSource, CancellationToken ct = default);
        Task<LearningProgressSummaryDto> RecalculateCourseProgressAsync(string courseId, Guid studentId, CancellationToken ct = default);
        Task<List<CourseStudentProgressDto>> GetCourseStudentProgressesAsync(string courseId, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<List<TopicProgressDto>> GetStudentTopicProgressesForCourseAsync(string courseId, Guid studentId, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<LearningProgressSyncResultDto> SyncCourseProgressFromResponsesAsync(string courseId, Guid requesterId, string? requesterRole, CancellationToken ct = default);
    }
}
