namespace DevelopmentTgBot.Notifications;

/// <summary>
/// Reason a notification request was rejected before it could be queued.
/// The endpoint maps each reason to the right HTTP status code so clients
/// can distinguish a typo (404) from a permission gap (403) from
/// back-pressure (503).
/// </summary>
public enum NotificationFailureReason
{
    UnknownDestination,
    ForbiddenDestination,
    QueueFull
}

public sealed class NotificationException : Exception
{
    public NotificationFailureReason Reason { get; }

    public NotificationException(NotificationFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }
}
