using DevelopmentTgBot.Configuration;

namespace DevelopmentTgBot.Telegram;

/// <summary>
/// Thin abstraction over the Bot API's <c>sendMessage</c> call. Kept as
/// an interface so the dispatcher can be unit-tested without an HTTP
/// dependency, and so Telegram can be swapped for a different transport
/// later.
/// </summary>
public interface ITelegramSender
{
    Task SendAsync(
        DestinationOptions destination,
        string text,
        string? parseMode,
        bool? disableNotification,
        CancellationToken cancellationToken);
}
