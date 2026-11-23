using LetsLearn.API.Services;

namespace LetsLearn.API.BackgroundServices;

public sealed class QuizBattleCompletionWorker(IServiceScopeFactory scopes, ILogger<QuizBattleCompletionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<QuizBattleService>().CompleteDueBattlesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Could not finalize due Quiz Battles; retrying next tick"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
