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

    /// <summary>
    /// Uploads a file to Telegram as a document via the Bot API's
    /// <c>sendDocument</c> endpoint. Used for things like daily DB backups
    /// that don't fit a 4096-char text message.
    ///
    /// <para><b>Streamed.</b> The caller hands us an open <see cref="Stream"/>
    /// (e.g. a FileStream over the dump on disk); we never buffer the
    /// whole file in memory. The caller owns the stream's lifetime.</para>
    /// </summary>
    Task SendDocumentAsync(
        DestinationOptions destination,
        Stream content,
        string fileName,
        string? caption,
        bool? disableNotification,
        CancellationToken cancellationToken);
}
