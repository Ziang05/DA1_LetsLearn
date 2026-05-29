using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.ServiceInterfaces;

namespace LetsLearn.API.BackgroundServices
{
    public class AiQuestionGenerationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAiQuestionGenerationJobQueue _queue;
        private readonly ILogger<AiQuestionGenerationBackgroundService> _logger;

        public AiQuestionGenerationBackgroundService(
            IServiceProvider serviceProvider,
            IAiQuestionGenerationJobQueue queue,
            ILogger<AiQuestionGenerationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[AI Questions] Background service started.");
            await RequeuePendingJobsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var jobId = await _queue.DequeueAsync(stoppingToken);
                    await ProcessJobAsync(jobId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AI Questions] Error while processing generation queue.");
                }
            }

            _logger.LogInformation("[AI Questions] Background service stopped.");
        }

        private async Task RequeuePendingJobsAsync(CancellationToken ct)
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var pendingJobs = (await uow.AiQuestionGenerationJobs.FindAsync(j => j.Status == "queued", ct))
                    .Where(j => j != null)
                    .Cast<AiQuestionGenerationJob>()
                    .ToList();

                foreach (var job in pendingJobs)
                {
                    await _queue.EnqueueAsync(job.Id, ct);
                }

                if (pendingJobs.Count > 0)
                {
                    _logger.LogInformation("[AI Questions] Requeued {Count} pending job(s).", pendingJobs.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AI Questions] Failed to requeue pending jobs.");
            }
        }

        private async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IAiQuestionGenerationService>();

            try
            {
                _logger.LogInformation("[AI Questions] Processing job {JobId}.", jobId);
                await service.ProcessGenerationJobAsync(jobId, ct);
                _logger.LogInformation("[AI Questions] Finished job {JobId}.", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AI Questions] Failed job {JobId}.", jobId);
            }
        }
    }
}
