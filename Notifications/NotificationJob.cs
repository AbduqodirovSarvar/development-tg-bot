using DevelopmentTgBot.Configuration;

namespace DevelopmentTgBot.Notifications;

/// <summary>
/// In-memory work item passed from the endpoint to the dispatcher. The
/// destination is captured here as a fully-resolved
/// <see cref="DestinationOptions"/> snapshot, not as a name — this way a
/// later config edit can't redirect an already-queued message to a
/// different chat.
/// </summary>
public sealed record NotificationJob(
    Guid Id,
    string ClientName,
    string DestinationName,
    DestinationOptions Destination,
    string Text,
    string? ParseMode,
    bool? DisableNotification);
