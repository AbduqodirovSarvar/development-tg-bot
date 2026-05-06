namespace DevelopmentTgBot.Contracts;

/// <summary>
/// Returned with HTTP 202 once the message is queued. The gateway is
/// fire-and-forget: a 202 means "we accepted it for delivery", not
/// "Telegram has it". The <see cref="Id"/> is a correlation handle that
/// shows up in logs for any later delivery failure.
/// </summary>
public sealed record SendNotificationResponse(Guid Id, string Destination);
