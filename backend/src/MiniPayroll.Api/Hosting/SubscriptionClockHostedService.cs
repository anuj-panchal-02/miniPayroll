using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Hosting;

public sealed class SubscriptionClockHostedService(
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<SubscriptionClockHostedService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        await TickAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var clock = scope.ServiceProvider.GetRequiredService<SubscriptionClock>();
            await clock.TickAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Subscription clock tick failed.");
        }
    }
}
