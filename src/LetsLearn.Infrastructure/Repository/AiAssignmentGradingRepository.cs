using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.Infrastructure.Repository
{
    public class AiAssignmentGradingRepository : GenericRepository<AiAssignmentGradingJob>, IAiAssignmentGradingRepository
    {
        public AiAssignmentGradingRepository(LetsLearnContext context) : base(context)
        {
        }

        public async Task<AiAssignmentGradingJob?> GetWithDraftByIdAsync(Guid jobId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Include(job => job.Draft)
                .FirstOrDefaultAsync(job => job.Id == jobId, ct);
        }

        public Task<AiAssignmentGradingJob?> GetTrackedByIdAsync(Guid jobId, CancellationToken ct = default)
        {
            return _dbSet.FirstOrDefaultAsync(job => job.Id == jobId, ct);
        }

        public async Task<AiAssignmentGradingJob?> GetReusableByInputHashAsync(
            string inputHash,
            CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Include(job => job.Draft)
                .Where(job => job.InputHash == inputHash
                    && (job.Status == AiAssignmentGradingJobStatuses.Queued
                        || job.Status == AiAssignmentGradingJobStatuses.Processing
                        || job.Status == AiAssignmentGradingJobStatuses.Succeeded))
                .OrderByDescending(job => job.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<AiAssignmentGradingJob?> GetReusableByRequestHashAsync(
            string requestHash,
            CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Include(job => job.Draft)
                .Where(job => job.RequestHash == requestHash
                    && (job.Status == AiAssignmentGradingJobStatuses.Queued
                        || job.Status == AiAssignmentGradingJobStatuses.Processing
                        || job.Status == AiAssignmentGradingJobStatuses.Succeeded))
                .OrderByDescending(job => job.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<AiAssignmentGradingJob?> GetLatestByAssignmentResponseIdAsync(
            Guid responseId,
            CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Include(job => job.Draft)
                .Where(job => job.AssignmentResponseId == responseId)
                .OrderByDescending(job => job.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        public Task<bool> HasJobsForAssignmentResponseAsync(Guid responseId, CancellationToken ct = default)
        {
            return _dbSet.AnyAsync(job => job.AssignmentResponseId == responseId, ct);
        }

        public async Task MarkStaleForAssignmentResponseAsync(Guid responseId, DateTime staleAt, CancellationToken ct = default)
        {
            var jobs = await _dbSet
                .Include(job => job.Draft)
                .Where(job => job.AssignmentResponseId == responseId
                    && (job.Status == AiAssignmentGradingJobStatuses.Queued
                        || job.Status == AiAssignmentGradingJobStatuses.Processing
                        || job.Status == AiAssignmentGradingJobStatuses.Succeeded))
                .ToListAsync(ct);

            foreach (var job in jobs)
            {
                job.Status = AiAssignmentGradingJobStatuses.Stale;
                job.CompletedAt = staleAt;
                job.LeaseExpiresAt = null;
                if (job.Draft != null && job.Draft.ReviewStatus == AiAssignmentGradeReviewStatuses.Pending)
                {
                    job.Draft.ReviewStatus = AiAssignmentGradeReviewStatuses.Stale;
                    job.Draft.ReviewVersion++;
                }
            }
        }

        public async Task<int> MarkStaleForTopicAsync(Guid topicId, DateTime staleAt, CancellationToken ct = default)
        {
            var rubricIds = await _context.AssignmentRubrics.AsNoTracking()
                .Where(rubric => rubric.TopicId == topicId)
                .Select(rubric => rubric.Id)
                .ToListAsync(ct);
            if (rubricIds.Count == 0) return 0;

            var jobs = await _dbSet.Include(job => job.Draft)
                .Where(job => rubricIds.Contains(job.RubricId)
                    && (job.Status == AiAssignmentGradingJobStatuses.Queued
                        || job.Status == AiAssignmentGradingJobStatuses.Processing
                        || (job.Status == AiAssignmentGradingJobStatuses.Succeeded
                            && job.Draft != null
                            && job.Draft.ReviewStatus == AiAssignmentGradeReviewStatuses.Pending)))
                .ToListAsync(ct);

            foreach (var job in jobs)
            {
                job.Status = AiAssignmentGradingJobStatuses.Stale;
                job.CompletedAt = staleAt;
                job.LeaseExpiresAt = null;
                job.LeaseToken = null;
                if (job.Draft?.ReviewStatus == AiAssignmentGradeReviewStatuses.Pending)
                {
                    job.Draft.ReviewStatus = AiAssignmentGradeReviewStatuses.Stale;
                    job.Draft.ReviewVersion++;
                }
            }
            return jobs.Count;
        }

        public async Task<(IReadOnlyList<AiAssignmentGradingJob> Jobs, int TotalCount)> GetHistoryPageAsync(
            Guid responseId,
            int skip,
            int take,
            CancellationToken ct = default)
        {
            var query = _dbSet.AsNoTracking().Where(job => job.AssignmentResponseId == responseId);
            var totalCount = await query.CountAsync(ct);
            var jobs = await query.Include(job => job.Draft)
                .OrderByDescending(job => job.CreatedAt)
                .ThenByDescending(job => job.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
            return (jobs, totalCount);
        }

        public Task<int> TryClaimQueuedJobAsync(
            Guid jobId,
            Guid leaseToken,
            DateTime startedAt,
            DateTime leaseExpiresAt,
            CancellationToken ct = default)
        {
            return _dbSet
                .Where(job => job.Id == jobId
                    && job.Status == AiAssignmentGradingJobStatuses.Queued
                    && job.AvailableAt <= startedAt)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.Status, AiAssignmentGradingJobStatuses.Processing)
                    .SetProperty(job => job.StartedAt, startedAt)
                    .SetProperty(job => job.LeaseExpiresAt, leaseExpiresAt)
                    .SetProperty(job => job.LeaseToken, leaseToken)
                    .SetProperty(job => job.Attempt, job => job.Attempt + 1), ct);
        }

        public async Task<IReadOnlyList<Guid>> GetReadyQueuedJobIdsAsync(DateTime now, int take, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(job => job.Status == AiAssignmentGradingJobStatuses.Queued && job.AvailableAt <= now)
                .OrderBy(job => job.AvailableAt)
                .ThenBy(job => job.CreatedAt)
                .Select(job => job.Id)
                .Take(take)
                .ToListAsync(ct);
        }

        public async Task RecoverExpiredLeasesAsync(DateTime now, int maxAttempts, CancellationToken ct = default)
        {
            await _dbSet
                .Where(job => job.Status == AiAssignmentGradingJobStatuses.Processing
                    && job.LeaseExpiresAt <= now
                    && job.Attempt >= maxAttempts)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.Status, AiAssignmentGradingJobStatuses.Failed)
                    .SetProperty(job => job.ErrorCode, "worker_lease_expired")
                    .SetProperty(job => job.ErrorMessage, "Worker bị dừng khi đang xử lý; job đã hết số lần thử.")
                    .SetProperty(job => job.CompletedAt, now)
                    .SetProperty(job => job.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(job => job.LeaseToken, (Guid?)null), ct);

            await _dbSet
                .Where(job => job.Status == AiAssignmentGradingJobStatuses.Processing
                    && job.LeaseExpiresAt <= now
                    && job.Attempt < maxAttempts)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.Status, AiAssignmentGradingJobStatuses.Queued)
                    .SetProperty(job => job.ErrorCode, "worker_lease_expired")
                    .SetProperty(job => job.ErrorMessage, "Worker bị dừng khi đang xử lý; job được đưa lại vào hàng đợi.")
                    .SetProperty(job => job.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(job => job.LeaseToken, (Guid?)null)
                    .SetProperty(job => job.AvailableAt, now), ct);
        }

        public Task<int> CompleteProcessingJobAsync(Guid jobId, Guid leaseToken, DateTime completedAt, CancellationToken ct = default)
        {
            return _dbSet
                .Where(job => job.Id == jobId
                    && job.Status == AiAssignmentGradingJobStatuses.Processing
                    && job.LeaseToken == leaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.Status, AiAssignmentGradingJobStatuses.Succeeded)
                    .SetProperty(job => job.CompletedAt, completedAt)
                    .SetProperty(job => job.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(job => job.LeaseToken, (Guid?)null)
                    .SetProperty(job => job.ErrorCode, (string?)null)
                    .SetProperty(job => job.ErrorMessage, (string?)null), ct);
        }

        public Task<int> SetProcessingSnapshotAsync(
            Guid jobId,
            Guid leaseToken,
            string submissionText,
            string submissionHash,
            string inputHash,
            CancellationToken ct = default)
        {
            return _dbSet
                .Where(job => job.Id == jobId
                    && job.Status == AiAssignmentGradingJobStatuses.Processing
                    && job.LeaseToken == leaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.SubmissionTextSnapshot, submissionText)
                    .SetProperty(job => job.SubmissionHash, submissionHash)
                    .SetProperty(job => job.InputHash, inputHash), ct);
        }

        public Task<int> MarkProcessingJobStaleAsync(Guid jobId, Guid leaseToken, DateTime staleAt, CancellationToken ct = default)
        {
            return _dbSet
                .Where(job => job.Id == jobId
                    && job.Status == AiAssignmentGradingJobStatuses.Processing
                    && job.LeaseToken == leaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.Status, AiAssignmentGradingJobStatuses.Stale)
                    .SetProperty(job => job.CompletedAt, staleAt)
                    .SetProperty(job => job.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(job => job.LeaseToken, (Guid?)null), ct);
        }

        public Task<int> SetProcessingJobFailureAsync(
            Guid jobId,
            Guid leaseToken,
            bool retry,
            string errorCode,
            string errorMessage,
            DateTime now,
            DateTime nextAttemptAt,
            CancellationToken ct = default)
        {
            var status = retry ? AiAssignmentGradingJobStatuses.Queued : AiAssignmentGradingJobStatuses.Failed;
            return _dbSet
                .Where(job => job.Id == jobId
                    && job.Status == AiAssignmentGradingJobStatuses.Processing
                    && job.LeaseToken == leaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.Status, status)
                    .SetProperty(job => job.ErrorCode, errorCode)
                    .SetProperty(job => job.ErrorMessage, errorMessage)
                    .SetProperty(job => job.CompletedAt, retry ? (DateTime?)null : now)
                    .SetProperty(job => job.AvailableAt, nextAttemptAt)
                    .SetProperty(job => job.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(job => job.LeaseToken, (Guid?)null), ct);
        }
    }
}
