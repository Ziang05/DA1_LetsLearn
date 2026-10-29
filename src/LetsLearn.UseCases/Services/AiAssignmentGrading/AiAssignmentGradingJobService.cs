using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LetsLearn.UseCases.Services.AiAssignmentGrading
{
    public sealed class AiAssignmentGradingJobService : IAiAssignmentGradingJobService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAiAssignmentGradingJobQueue _queue;
        private readonly IAiQuotaService _quotaService;
        private readonly IConfiguration _configuration;

        public AiAssignmentGradingJobService(
            IUnitOfWork unitOfWork,
            IAiAssignmentGradingJobQueue queue,
            IAiQuotaService quotaService,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _queue = queue;
            _quotaService = quotaService;
            _configuration = configuration;
        }

        public async Task<AiAssignmentGradingJobRequestResult> RequestAsync(
            Guid assignmentResponseId,
            Guid requesterId,
            CancellationToken ct = default)
        {
            if (assignmentResponseId == Guid.Empty || requesterId == Guid.Empty)
            {
                throw new ArgumentException("Assignment response and requester IDs are required.");
            }

            var submission = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(assignmentResponseId)
                ?? throw new KeyNotFoundException("Assignment response not found.");
            var rubric = await _unitOfWork.AssignmentRubrics.GetLatestByTopicIdAsync(submission.TopicId, ct)
                ?? throw new AssignmentGradingException("rubric_not_configured", "Chưa cấu hình rubric cho bài tập này.");
            var assignment = await _unitOfWork.TopicAssignments.GetByIdAsync(submission.TopicId, ct)
                ?? throw new KeyNotFoundException("Assignment not found.");
            var rubricJson = AssignmentGradingInputHasher.SerializeRubric(rubric);
            var model = ResolveModel();
            var description = assignment.Description ?? string.Empty;
            var promptVersion = _configuration["AIGrading:PromptVersion"] ?? "grading-v1";
            var requestHash = AssignmentGradingInputHasher.ComputeRequestHash(submission, description, rubricJson, promptVersion, model);

            var existing = await _unitOfWork.AiAssignmentGradingJobs.GetReusableByRequestHashAsync(requestHash, ct);
            if (existing != null)
            {
                return ToRequestResult(existing, reused: true);
            }

            await EnforceQuotaAsync(requesterId, "assignment_grading_per_minute", GetQuota("RequestsPerMinute", 5), TimeSpan.FromMinutes(1), ct);
            await EnforceQuotaAsync(requesterId, "assignment_grading_per_day", GetQuota("RequestsPerDay", 50), TimeSpan.FromDays(1), ct);

            var now = DateTime.UtcNow;
            var job = new AiAssignmentGradingJob
            {
                Id = Guid.NewGuid(),
                AssignmentResponseId = submission.Id,
                RequestedBy = requesterId,
                RubricId = rubric.Id,
                RubricVersion = rubric.Version,
                AssignmentDescriptionSnapshot = description,
                // InputHash is replaced with the content-derived hash by the worker after extraction.
                InputHash = requestHash,
                RequestHash = requestHash,
                PromptVersion = promptVersion,
                Model = model,
                Status = AiAssignmentGradingJobStatuses.Queued,
                Attempt = 0,
                CreatedAt = now,
                AvailableAt = now
            };

            await _unitOfWork.AiAssignmentGradingJobs.AddAsync(job);
            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request may win the partial unique index for this input hash.
                // Return that durable job if it now exists; otherwise let the actual DB error surface.
                existing = await _unitOfWork.AiAssignmentGradingJobs.GetReusableByRequestHashAsync(requestHash, ct);
                if (existing == null) throw;
                return ToRequestResult(existing, reused: true);
            }

            // The DB row is committed first. If this signal is lost, the recovery scanner
            // finds the queued job and sends another signal.
            await _queue.EnqueueAsync(job.Id, ct);
            return ToRequestResult(job, reused: false);
        }

        private string ResolveModel() =>
            _configuration["AI:AssignmentGradingModel"]
            ?? _configuration["AI:GeneratorModel"]
            ?? (string.Equals(_configuration["AI:Provider"], "Gemini", StringComparison.OrdinalIgnoreCase)
                ? "gemini-2.5-flash"
                : "gpt-4o-mini");

        private async Task EnforceQuotaAsync(Guid userId, string metric, int limit, TimeSpan window, CancellationToken ct)
        {
            var enabled = bool.TryParse(_configuration["AIQuota:Enabled"], out var configuredEnabled)
                ? configuredEnabled
                : true;
            if (enabled)
            {
                await _quotaService.CheckAndConsumeAsync(userId, metric, 1, limit, window, ct);
            }
        }

        private int GetQuota(string key, int defaultValue) =>
            int.TryParse(_configuration[$"AIGrading:{key}"], out var configured) && configured > 0
                ? configured
                : defaultValue;

        private static AiAssignmentGradingJobRequestResult ToRequestResult(AiAssignmentGradingJob job, bool reused) => new()
        {
            JobId = job.Id,
            Status = job.Status,
            CreatedAt = job.CreatedAt,
            ReusedExistingJob = reused
        };
    }
}
