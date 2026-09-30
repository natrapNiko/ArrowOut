using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace ArrowOut.Services.Analytics;

// Small in-memory queue between the requests and the background sender.
// If it fills up we drop the oldest event. Analytics should never slow the game down.
public sealed class AnalyticsQueue
{
    public AnalyticsQueue(int capacity)
    {
        Channel = System.Threading.Channels.Channel.CreateBounded<AnalyticsEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public Channel<AnalyticsEvent> Channel { get; }
}

public sealed class ChannelAnalyticsTracker(
    AnalyticsQueue queue,
    TimeProvider timeProvider,
    ILogger<ChannelAnalyticsTracker> logger) : IAnalyticsTracker
{
    public void Track(string eventName, string userId, IReadOnlyDictionary<string, object?>? properties = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var analyticsEvent = new AnalyticsEvent(
                eventName,
                Pseudonymise(userId),
                properties ?? new Dictionary<string, object?>(),
                timeProvider.GetUtcNow());

            if (!queue.Channel.Writer.TryWrite(analyticsEvent))
            {
                logger.LogDebug("Analytics queue rejected event {Event}", eventName);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // If tracking fails, too bad. It's not worth breaking a request over.
            logger.LogWarning(ex, "Failed to enqueue analytics event {Event}", eventName);
        }
    }

    // PostHog only ever gets a hash, never the real user id or e-mail.
    internal static string Pseudonymise(string userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("arrowout:" + userId));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }
}
