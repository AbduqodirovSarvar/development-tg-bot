using System.Net.Http.Json;
using DevelopmentTgBot.Configuration;
using Microsoft.Extensions.Options;

namespace DevelopmentTgBot.Telegram;

/// <summary>
/// Calls Telegram Bot API's <c>sendMessage</c> via a typed HttpClient.
/// The client itself is configured in Program.cs with the resilience
/// pipeline (retries on transient HTTP errors), so this class stays
/// focused on payload shape and response parsing.
///
/// <para>The bot token is read from options on every call instead of
/// being baked into the BaseAddress at startup. This lets the deployer
/// rotate the token via config-reload without restarting the process.</para>
/// </summary>
public sealed class TelegramSender : ITelegramSender
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ILogger<TelegramSender> _logger;

    public TelegramSender(
        HttpClient http,
        IOptionsMonitor<TelegramOptions> options,
        ILogger<TelegramSender> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(
        DestinationOptions destination,
        string text,
        string? parseMode,
        bool? disableNotification,
        CancellationToken cancellationToken)
    {
        var current = _options.CurrentValue;

        // Dry-run short-circuit for local Postman / integration testing.
        // Logged at Information so the round-trip is visible in the console
        // without lifting the default log level.
        if (current.DryRun)
        {
            _logger.LogInformation(
                "[DRY RUN] Would send to chat {ChatId} (topic {TopicId}, parseMode {ParseMode}, silent {Silent}): {Text}",
                destination.ChatId, destination.TopicId, parseMode ?? "(none)",
                disableNotification == true, text);
            return;
        }

        var token = current.BotToken;
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException(
                "Telegram bot token is not configured. Set Telegram:BotToken via env vars or user secrets, " +
                "or enable Telegram:DryRun for local testing.");

        // Build a dictionary so we only include optional fields when set —
        // Telegram's sendMessage rejects unrecognized null fields in some
        // edge cases, and an explicit message_thread_id of null would
        // override forum routing in unexpected ways.
        var payload = new Dictionary<string, object>
        {
            ["chat_id"] = destination.ChatId,
            ["text"] = text
        };

        if (destination.TopicId.HasValue)
            payload["message_thread_id"] = destination.TopicId.Value;

        if (!string.IsNullOrEmpty(parseMode))
            payload["parse_mode"] = parseMode;

        if (disableNotification == true)
            payload["disable_notification"] = true;

        var url = $"/bot{token}/sendMessage";
        using var response = await _http.PostAsJsonAsync(url, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Read the body so the dispatcher's log line includes Telegram's
            // own error description ("Bad Request: chat not found", etc.)
            // rather than a generic 4xx — that's the difference between a
            // misconfigured destination and a transient outage.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Telegram sendMessage to chat {ChatId} (topic {TopicId}) failed: {StatusCode} {Body}",
                destination.ChatId, destination.TopicId, (int)response.StatusCode, body);

            response.EnsureSuccessStatusCode();
        }
    }

    public async Task SendDocumentAsync(
        DestinationOptions destination,
        Stream content,
        string fileName,
        string? caption,
        bool? disableNotification,
        CancellationToken cancellationToken)
    {
        var current = _options.CurrentValue;

        if (current.DryRun)
        {
            _logger.LogInformation(
                "[DRY RUN] Would upload document {FileName} to chat {ChatId} (topic {TopicId}, caption {Caption}).",
                fileName, destination.ChatId, destination.TopicId, caption ?? "(none)");
            return;
        }

        var token = current.BotToken;
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException(
                "Telegram bot token is not configured. Set Telegram:BotToken via env vars or user secrets, " +
                "or enable Telegram:DryRun for local testing.");

        // multipart/form-data — Bot API requires this for file uploads.
        // Each scalar field becomes its own StringContent so Telegram parses
        // the form correctly; the binary file goes through StreamContent
        // so we never buffer it into a byte[] in memory.
        using var form = new MultipartFormDataContent
        {
            { new StringContent(destination.ChatId), "chat_id" }
        };

        if (destination.TopicId.HasValue)
            form.Add(new StringContent(destination.TopicId.Value.ToString()), "message_thread_id");

        if (!string.IsNullOrEmpty(caption))
            form.Add(new StringContent(caption), "caption");

        if (disableNotification == true)
            form.Add(new StringContent("true"), "disable_notification");

        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        // The third arg to Add is the field name expected by Bot API
        // (`document`), the fourth is the file name surfaced in Telegram.
        form.Add(fileContent, "document", fileName);

        var url = $"/bot{token}/sendDocument";
        using var response = await _http.PostAsync(url, form, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Telegram sendDocument to chat {ChatId} (file {FileName}) failed: {StatusCode} {Body}",
                destination.ChatId, fileName, (int)response.StatusCode, body);

            response.EnsureSuccessStatusCode();
        }
    }
}
