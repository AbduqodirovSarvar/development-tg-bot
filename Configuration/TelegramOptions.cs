namespace DevelopmentTgBot.Configuration;

/// <summary>
/// Bot-side configuration: token + base URL. The token must come from a
/// secret source (env var / user secrets / vault) — never commit it to
/// appsettings.json. <see cref="BaseUrl"/> is split out so tests can point
/// at a fake server.
/// </summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.telegram.org";

    /// <summary>
    /// Per-attempt timeout for the Bot API call. Telegram is usually fast;
    /// long timeouts mostly hide upstream issues that we'd rather see and
    /// retry on.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// When true, <see cref="Telegram.TelegramSender"/> logs the would-be
    /// Bot API call instead of making it. Intended for local Postman /
    /// integration testing — lets you exercise auth, queueing, and the
    /// dispatcher pipeline without a real bot token or chat. Should be
    /// off in any environment where an accidental "send for real" would
    /// be embarrassing.
    /// </summary>
    public bool DryRun { get; set; }
}
