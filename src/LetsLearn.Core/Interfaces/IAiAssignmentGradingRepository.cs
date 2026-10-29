using LetsLearn.Core.Entities;

namespace LetsLearn.Core.Interfaces
{
    public interface IAiAssignmentGradingRepository : IRepository<AiAssignmentGradingJob>
    {
        Task<AiAssignmentGradingJob?> GetWithDraftByIdAsync(Guid jobId, CancellationToken ct = default);
        Task<AiAssignmentGradingJob?> GetReusableByInputHashAsync(string inputHash, CancellationToken ct = default);
        Task<AiAssignmentGradingJob?> GetReusableByRequestHashAsync(string requestHash, CancellationToken ct = default);
        Task<AiAssignmentGradingJob?> GetLatestByAssignmentResponseIdAsync(Guid responseId, CancellationToken ct = default);
        Task<bool> HasJobsForAssignmentResponseAsync(Guid responseId, CancellationToken ct = default);
        Task MarkStaleForAssignmentResponseAsync(Guid responseId, DateTime staleAt, CancellationToken ct = default);
        Task<int> MarkStaleForTopicAsync(Guid topicId, DateTime staleAt, CancellationToken ct = default);
        Task<(IReadOnlyList<AiAssignmentGradingJob> Jobs, int TotalCount)> GetHistoryPageAsync(Guid responseId, int skip, int take, CancellationToken ct = default);
        Task<IReadOnlyList<Guid>> GetReadyQueuedJobIdsAsync(DateTime now, int take, CancellationToken ct = default);
        Task RecoverExpiredLeasesAsync(DateTime now, int maxAttempts, CancellationToken ct = default);
        Task<AiAssignmentGradingJob?> GetTrackedByIdAsync(Guid jobId, CancellationToken ct = default);
        Task<int> TryClaimQueuedJobAsync(Guid jobId, Guid leaseToken, DateTime startedAt, DateTime leaseExpiresAt, CancellationToken ct = default);
        Task<int> CompleteProcessingJobAsync(Guid jobId, Guid leaseToken, DateTime completedAt, CancellationToken ct = default);
        Task<int> MarkProcessingJobStaleAsync(Guid jobId, Guid leaseToken, DateTime staleAt, CancellationToken ct = default);
        Task<int> SetProcessingJobFailureAsync(Guid jobId, Guid leaseToken, bool retry, string errorCode, string errorMessage, DateTime now, DateTime nextAttemptAt, CancellationToken ct = default);
        Task<int> SetProcessingSnapshotAsync(Guid jobId, Guid leaseToken, string submissionText, string submissionHash, string inputHash, CancellationToken ct = default);
    }
}
