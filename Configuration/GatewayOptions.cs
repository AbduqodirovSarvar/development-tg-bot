namespace DevelopmentTgBot.Configuration;

/// <summary>
/// Top-level gateway configuration: the destination map and the API client
/// list. Bound from the <c>Gateway</c> section of appsettings.json so the
/// public, non-secret topology lives in source control while bot tokens
/// and API keys are sourced from env vars / user secrets in production.
/// </summary>
public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    /// <summary>
    /// Destination name (e.g. <c>familytree.bugs</c>) → resolved chat/topic.
    /// Lookup is case-insensitive — see options validation in Program.cs.
    /// </summary>
    public Dictionary<string, DestinationOptions> Destinations { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);

    public List<ApiClientOptions> ApiClients { get; set; } = new();

    /// <summary>
    /// Bounded queue size between the endpoint and the dispatcher. When
    /// full, new requests are rejected with 503 — this is the back-pressure
    /// signal that tells callers Telegram is overwhelmed (or the gateway
    /// is). 1024 is intentionally modest; raise it after measuring real
    /// traffic.
    /// </summary>
    public int QueueCapacity { get; set; } = 1024;

    /// <summary>How many times to retry a failed Bot API call before dropping the message.</summary>
    public int MaxSendRetries { get; set; } = 3;
}
