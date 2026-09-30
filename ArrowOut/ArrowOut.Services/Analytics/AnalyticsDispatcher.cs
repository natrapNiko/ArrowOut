using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArrowOut.Services.Analytics;

// Takes events off the queue in the background and sends them with IAnalyticsClient.
// The client is created per event from a new scope. Typed HttpClients are short-lived, and
// holding one forever in a singleton would keep the same handler and miss DNS changes.
public sealed class AnalyticsDispatcher(
    AnalyticsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AnalyticsDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var analyticsEvent in queue.Channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var client = scope.ServiceProvider.GetRequiredService<IAnalyticsClient>();
                    await client.SendAsync(analyticsEvent, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // Retries already happened in the HttpClient, so just drop this event and carry on.
                    logger.LogWarning(ex, "Analytics event {Event} could not be delivered", analyticsEvent.Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // App is shutting down, that's fine.
        }
    }
}
