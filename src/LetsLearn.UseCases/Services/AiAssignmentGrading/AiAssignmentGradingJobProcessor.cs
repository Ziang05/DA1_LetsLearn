using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LetsLearn.UseCases.Services.AiAssignmentGrading
{
    public sealed class AiAssignmentGradingJobProcessor : IAiAssignmentGradingJobProcessor
    {
        private const int DefaultMaxAttempts = 3;
        private const int DefaultLeaseSeconds = 120;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAssignmentSubmissionExtractor _submissionExtractor;
        private readonly IAssignmentGradingService _gradingService;
        private readonly IAiAssignmentGradingJobQueue _queue;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiAssignmentGradingJobProcessor> _logger;

        public AiAssignmentGradingJobProcessor(
            IUnitOfWork unitOfWork,
            IAssignmentSubmissionExtractor submissionExtractor,
            IAssignmentGradingService gradingService,
            IAiAssignmentGradingJobQueue queue,
            IConfiguration configuration,
            ILogger<AiAssignmentGradingJobProcessor> logger)
        {
            _unitOfWork = unitOfWork;
            _submissionExtractor = submissionExtractor;
            _gradingService = gradingService;
            _queue = queue;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task ProcessAsync(Guid jobId, CancellationToken ct = default)
        {
            var maxAttempts = GetPositiveSetting("AIGrading:MaxAttempts", DefaultMaxAttempts);
            var leaseSeconds = Math.Clamp(GetPositiveSetting("AIGrading:LeaseSeconds", DefaultLeaseSeconds), 30, 600);
            var startedAt = DateTime.UtcNow;
            var leaseToken = Guid.NewGuid();
            var claimed = await _unitOfWork.AiAssignmentGradingJobs.TryClaimQueuedJobAsync(
                jobId,
                leaseToken,
                startedAt,
                startedAt.AddSeconds(leaseSeconds),
                ct);
            if (claimed == 0) return;

            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("AI assignment grading job claimed. JobId={JobId}", jobId);
            var job = await _unitOfWork.AiAssignmentGradingJobs.GetWithDraftByIdAsync(jobId, ct);
            if (job == null) return;

            try
            {
                var initial = await LoadAndValidateSnapshotAsync(job, ct);
                if (initial == null)
                {
                    await MarkStaleAsync(jobId, leaseToken, ct);
                    _logger.LogInformation("AI assignment grading job became stale. JobId={JobId} Stage={Stage}", jobId, "before_model");
                    return;
                }

                var inputHash = AssignmentGradingInputHasher.ComputeInputHash(initial.Input);
                var reusable = await _unitOfWork.AiAssignmentGradingJobs.GetReusableByInputHashAsync(inputHash, ct);
                if (reusable != null && reusable.Id != job.Id)
                {
                    await _unitOfWork.AiAssignmentGradingJobs.SetProcessingJobFailureAsync(
                        job.Id, leaseToken, retry: false, "duplicate_input", "Đã có job khác cho cùng đầu vào.",
                        DateTime.UtcNow, DateTime.UtcNow, ct);
                    return;
                }

                try
                {
                    var snapshotSaved = await _unitOfWork.AiAssignmentGradingJobs.SetProcessingSnapshotAsync(
                        job.Id, leaseToken, initial.Input.SubmissionText, initial.SubmissionHash, inputHash, ct);
                    if (snapshotSaved == 0) return;
                }
                catch (Exception)
                {
                    reusable = await _unitOfWork.AiAssignmentGradingJobs.GetReusableByInputHashAsync(inputHash, ct);
                    if (reusable == null || reusable.Id == job.Id) throw;
                    await _unitOfWork.AiAssignmentGradingJobs.SetProcessingJobFailureAsync(
                        job.Id, leaseToken, retry: false, "duplicate_input", "Đã có job khác cho cùng đầu vào.",
                        DateTime.UtcNow, DateTime.UtcNow, ct);
                    return;
                }

                var input = initial.Input;
                var validatedGrade = await _gradingService.GradeAsync(input, initial.Rubric, ct);

                var saved = await _unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var currentSubmission = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(job.AssignmentResponseId);
                    var currentRubric = await _unitOfWork.AssignmentRubrics.GetLatestByTopicIdAsync(initial.TopicId, token);
                    var currentAssignment = await _unitOfWork.TopicAssignments.GetByIdAsync(initial.TopicId, token);
                    var stillMatches = currentSubmission != null
                        && currentRubric != null
                        && currentRubric.Id == job.RubricId
                        && currentRubric.Version == job.RubricVersion
                        && currentAssignment != null
                        && string.Equals(currentAssignment.Description ?? string.Empty, job.AssignmentDescriptionSnapshot, StringComparison.Ordinal)
                        && string.Equals(ComputeSubmissionMetadataHash(currentSubmission), initial.SubmissionMetadataHash, StringComparison.Ordinal);

                    if (!stillMatches)
                    {
                        await _unitOfWork.AiAssignmentGradingJobs.MarkProcessingJobStaleAsync(jobId, leaseToken, DateTime.UtcNow, token);
                        return false;
                    }

                    var completed = await _unitOfWork.AiAssignmentGradingJobs.CompleteProcessingJobAsync(jobId, leaseToken, DateTime.UtcNow, token);
                    if (completed == 0) return false;

                    await _unitOfWork.AiAssignmentGradeDrafts.AddAsync(validatedGrade.ToDraft(jobId));
                    return true;
                }, ct);

                stopwatch.Stop();
                if (saved)
                {
                    _logger.LogInformation("AI assignment grading job completed. JobId={JobId} ElapsedMs={ElapsedMs}", jobId, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogInformation("AI assignment grading result was not saved because the job became stale or lost its lease. JobId={JobId}", jobId);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The lease expires and is recovered by the polling loop after shutdown.
                throw;
            }
            catch (AssignmentGradingException ex)
            {
                await HandleFailureAsync(job, leaseToken, ex.Code, ex.Message, ex.Code == "provider_error", maxAttempts, stopwatch, ct);
            }
            catch (AssignmentSubmissionExtractionException ex)
            {
                await HandleFailureAsync(job, leaseToken, ex.Code, ex.Message, retry: false, maxAttempts, stopwatch, ct);
            }
            catch (Exception ex)
            {
                // Do not log exception text: provider/storage exceptions can contain URLs or response data.
                _logger.LogError("AI assignment grading job failed unexpectedly. JobId={JobId} ExceptionType={ExceptionType}", jobId, ex.GetType().Name);
                await HandleFailureAsync(job, leaseToken, "processing_error", "Lỗi xử lý nội bộ khi chấm bài.", retry: false, maxAttempts, stopwatch, ct);
            }
        }

        private async Task<CurrentSnapshot?> LoadAndValidateSnapshotAsync(AiAssignmentGradingJob job, CancellationToken ct)
        {
            var submission = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(job.AssignmentResponseId);
            if (submission == null) return null;
            var topicId = submission.TopicId;
            var latestRubric = await _unitOfWork.AssignmentRubrics.GetLatestByTopicIdAsync(topicId, ct);
            if (latestRubric == null || latestRubric.Id != job.RubricId || latestRubric.Version != job.RubricVersion)
            {
                return null;
            }

            var assignment = await _unitOfWork.TopicAssignments.GetByIdAsync(topicId, ct);
            if (assignment == null || !string.Equals(assignment.Description ?? string.Empty, job.AssignmentDescriptionSnapshot, StringComparison.Ordinal))
            {
                return null;
            }

            var rubricJson = AssignmentGradingInputHasher.SerializeRubric(latestRubric);
            var currentRequestHash = AssignmentGradingInputHasher.ComputeRequestHash(
                submission,
                assignment.Description ?? string.Empty,
                rubricJson,
                job.PromptVersion,
                job.Model);
            if (!string.Equals(currentRequestHash, job.RequestHash, StringComparison.Ordinal)) return null;

            var extracted = await _submissionExtractor.ExtractAsync(submission, ct);
            var input = new AssignmentGradingInput
            {
                AssignmentResponseId = job.AssignmentResponseId,
                SubmissionText = extracted.Text,
                AssignmentDescription = assignment.Description ?? string.Empty,
                RubricId = latestRubric.Id,
                RubricVersion = latestRubric.Version,
                RubricJson = rubricJson,
                PromptVersion = job.PromptVersion,
                Model = job.Model
            };
            return new CurrentSnapshot(topicId, latestRubric, input, extracted.ContentHash, ComputeSubmissionMetadataHash(submission));
        }

        private async Task MarkStaleAsync(Guid jobId, Guid leaseToken, CancellationToken ct)
        {
            await _unitOfWork.AiAssignmentGradingJobs.MarkProcessingJobStaleAsync(jobId, leaseToken, DateTime.UtcNow, ct);
        }

        private async Task HandleFailureAsync(
            AiAssignmentGradingJob job,
            Guid leaseToken,
            string errorCode,
            string errorMessage,
            bool retry,
            int maxAttempts,
            Stopwatch stopwatch,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            retry = retry && job.Attempt < maxAttempts;
            var backoffSeconds = retry ? Math.Min(30, 1 << Math.Min(job.Attempt, 5)) : 0;
            var changed = await _unitOfWork.AiAssignmentGradingJobs.SetProcessingJobFailureAsync(
                job.Id,
                leaseToken,
                retry,
                errorCode,
                errorMessage.Length <= 1000 ? errorMessage : errorMessage[..1000],
                now,
                now.AddSeconds(backoffSeconds),
                ct);
            stopwatch.Stop();
            _logger.LogWarning(
                "AI assignment grading job ended with error. JobId={JobId} ErrorCode={ErrorCode} Retrying={Retrying} Attempt={Attempt} ElapsedMs={ElapsedMs}",
                job.Id,
                errorCode,
                retry && changed > 0,
                job.Attempt,
                stopwatch.ElapsedMilliseconds);

            if (retry && changed > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), ct);
                await _queue.EnqueueAsync(job.Id, ct);
            }
        }

        private static string ComputeSubmissionMetadataHash(AssignmentResponse submission)
        {
            var values = new List<string>
            {
                submission.SubmittedAt?.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
            };
            values.AddRange(submission.Files.OrderBy(file => file.Id).Select(file =>
                $"{file.Id:D}|{file.Name ?? string.Empty}|{file.DownloadUrl ?? string.Empty}"));
            var canonical = string.Concat(values.Select(value => $"{value.Length}:{value}"));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }

        private int GetPositiveSetting(string key, int fallback) =>
            int.TryParse(_configuration[key], out var value) && value > 0 ? value : fallback;

        private sealed record CurrentSnapshot(Guid TopicId, AssignmentRubric Rubric, AssignmentGradingInput Input, string SubmissionHash, string SubmissionMetadataHash);
    }
}
