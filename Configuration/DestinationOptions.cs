namespace DevelopmentTgBot.Configuration;

/// <summary>
/// Named destination — a logical alias the client sends instead of raw
/// chat/topic IDs. Storing the mapping server-side keeps secrets-ish IDs
/// out of client code and lets us re-map ("familytree.bugs" → different
/// chat) without redeploying every consumer.
///
/// <para><b>TopicId is optional.</b> If null, the message is sent to the
/// chat's default thread (in a forum group that's the "General" topic;
/// in a regular group it's just the main chat).</para>
/// </summary>
public sealed class DestinationOptions
{
    public string ChatId { get; set; } = string.Empty;

    public int? TopicId { get; set; }

    /// <summary>Optional human-readable note — surfaced in logs only.</summary>
    public string? Description { get; set; }
}
