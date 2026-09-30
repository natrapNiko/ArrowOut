using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArrowOut.Services.Analytics;

// Sends events to PostHog's capture endpoint.
// Registered as a typed HttpClient with the standard retry/timeout/circuit-breaker setup.
public sealed class PostHogAnalyticsClient(
    HttpClient httpClient,
    IOptions<AnalyticsOptions> options,
    ILogger<PostHogAnalyticsClient> logger) : IAnalyticsClient
{
    public async Task SendAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analyticsEvent);

        var payload = new CapturePayload(
            options.Value.ApiKey ?? string.Empty,
            analyticsEvent.Name,
            analyticsEvent.DistinctId,
            analyticsEvent.Properties,
            analyticsEvent.Timestamp);

        using var response = await httpClient.PostAsJsonAsync("capture/", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "PostHog rejected event {Event} with status {Status}", analyticsEvent.Name, (int)response.StatusCode);
        }
    }

    private sealed record CapturePayload(
        [property: JsonPropertyName("api_key")] string ApiKey,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("distinct_id")] string DistinctId,
        [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, object?> Properties,
        [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
}

// Used when analytics is off. Does nothing.
public sealed class NullAnalyticsClient(ILogger<NullAnalyticsClient> logger) : IAnalyticsClient
{
    public Task SendAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analyticsEvent);
        logger.LogDebug("Analytics disabled; event {Event} discarded", analyticsEvent.Name);
        return Task.CompletedTask;
    }
}
