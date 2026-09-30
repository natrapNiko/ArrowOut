using System.ComponentModel.DataAnnotations;

namespace ArrowOut.Services.Analytics;

public sealed record AnalyticsEvent(
    string Name,
    string DistinctId,
    IReadOnlyDictionary<string, object?> Properties,
    DateTimeOffset Timestamp);

// What the app calls to track something. Never blocks and never throws.
public interface IAnalyticsTracker
{
    void Track(string eventName, string userId, IReadOnlyDictionary<string, object?>? properties = null);
}

// Actually sends the events somewhere. Which one is used depends on the config.
public interface IAnalyticsClient
{
    Task SendAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken);
}

public sealed class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    public bool Enabled { get; set; }

    // PostHog project key. It's write-only and meant to be public, so it's fine in config.
    public string? ApiKey { get; set; }

    [Url]
    public string Host { get; set; } = "https://eu.i.posthog.com";

    [Range(10, 10_000)]
    public int QueueCapacity { get; set; } = 1_000;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
