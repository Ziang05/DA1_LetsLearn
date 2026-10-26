using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.Configuration;

namespace LetsLearn.API.BackgroundServices
{
    public sealed class AiAssignmentGradingBackgroundService : BackgroundService
    {
        private const int QueueScanBatchSize = 250;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAiAssignmentGradingJobQueue _queue;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiAssignmentGradingBackgroundService> _logger;

        public AiAssignmentGradingBackgroundService(
            IServiceScopeFactory scopeFactory,
            IAiAssignmentGradingJobQueue queue,
            IConfiguration configuration,
            ILogger<AiAssignmentGradingBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _queue = queue;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AI assignment grading worker started.");
            await RecoverAndEnqueueAsync(stoppingToken);
            await Task.WhenAll(ConsumeQueueAsync(stoppingToken), RecoverPeriodicallyAsync(stoppingToken));
            _logger.LogInformation("AI assignment grading worker stopped.");
        }

        private async Task ConsumeQueueAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                Guid jobId;
                try
                {
                    jobId = await _queue.DequeueAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IAiAssignmentGradingJobProcessor>();
                    await processor.ProcessAsync(jobId, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("AI assignment grading worker could not process a job. JobId={JobId} ExceptionType={ExceptionType}", jobId, ex.GetType().Name);
                }
            }
        }

        private async Task RecoverPeriodicallyAsync(CancellationToken ct)
        {
            var scanSeconds = int.TryParse(_configuration["AIGrading:RecoveryIntervalSeconds"], out var configured) && configured > 0
                ? Math.Clamp(configured, 5, 300)
                : 15;
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(scanSeconds));
            while (await timer.WaitForNextTickAsync(ct))
            {
                await RecoverAndEnqueueAsync(ct);
            }
        }

        private async Task RecoverAndEnqueueAsync(CancellationToken ct)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var maxAttempts = int.TryParse(_configuration["AIGrading:MaxAttempts"], out var max) && max > 0 ? max : 3;
                await uow.AiAssignmentGradingJobs.RecoverExpiredLeasesAsync(DateTime.UtcNow, maxAttempts, ct);
                var jobIds = await uow.AiAssignmentGradingJobs.GetReadyQueuedJobIdsAsync(DateTime.UtcNow, QueueScanBatchSize, ct);
                foreach (var jobId in jobIds)
                {
                    await _queue.EnqueueAsync(jobId, ct);
                }
                if (jobIds.Count > 0)
                {
                    _logger.LogInformation("AI assignment grading recovery queued jobs. Count={Count}", jobIds.Count);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AI assignment grading recovery scan failed. ExceptionType={ExceptionType}", ex.GetType().Name);
            }
        }
    }
}
