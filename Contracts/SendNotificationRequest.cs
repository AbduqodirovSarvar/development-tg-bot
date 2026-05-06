using System.ComponentModel.DataAnnotations;

namespace DevelopmentTgBot.Contracts;

/// <summary>
/// Payload posted by client services to <c>POST /api/notify</c>.
/// <see cref="Destination"/> is a logical name that the gateway resolves
/// to a chat/topic — clients never see raw IDs.
/// </summary>
public sealed class SendNotificationRequest
{
    [Required]
    [MaxLength(128)]
    public string Destination { get; set; } = string.Empty;

    [Required]
    // Telegram caps a single sendMessage at 4096 chars; reject early so we
    // don't waste a queue slot and a Bot API round-trip on a doomed payload.
    [MaxLength(4096)]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Optional Telegram parse mode: <c>HTML</c>, <c>Markdown</c>, or
    /// <c>MarkdownV2</c>. Null means plain text — the safe default,
    /// since malformed markdown causes Telegram to reject the message
    /// outright.
    /// </summary>
    public string? ParseMode { get; set; }

    /// <summary>
    /// Suppress the notification sound on the client. Useful for noisy
    /// channels (e.g. statistics, ci/cd) so the group doesn't beep all day.
    /// </summary>
    public bool? DisableNotification { get; set; }
}
