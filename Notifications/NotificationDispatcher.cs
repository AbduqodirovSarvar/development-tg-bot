using DevelopmentTgBot.Configuration;
using DevelopmentTgBot.Telegram;
using Microsoft.Extensions.Options;

namespace DevelopmentTgBot.Notifications;

/// <summary>
/// Background worker that drains the in-memory queue and forwards each
/// job to Telegram. Runs as a single-reader loop — keeps ordering per
/// destination and avoids the "burst hits Telegram rate limits" problem
/// you'd get from a fan-out worker pool.
///
/// <para><b>Retry strategy.</b> The typed HttpClient already has the
/// .NET resilience pipeline (transient HTTP retries) wired up in
/// Program.cs. This loop layers a coarser application-level retry on
/// top — if the Bot API returns a non-success after the HTTP retries
/// have run, we wait and try the whole call up to
/// <see cref="GatewayOptions.MaxSendRetries"/> times before dropping
/// the message and logging an error. Dropping is a deliberate choice:
/// notifications are losable, blocking the queue isn't.</para>
/// </summary>
public sealed class NotificationDispatcher : BackgroundService
{
    private readonly NotificationQueue _queue;
    private readonly ITelegramSender _sender;
    private readonly IOptionsMonitor<GatewayOptions> _options;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        NotificationQueue queue,
        ITelegramSender sender,
        IOptionsMonitor<GatewayOptions> options,
        ILogger<NotificationDispatcher> logger)
    {
        _queue = queue;
        _sender = sender;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification dispatcher started.");

        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await DispatchWithRetryAsync(job, stoppingToken);
        }
    }

    private async Task DispatchWithRetryAsync(NotificationJob job, CancellationToken cancellationToken)
    {
        var maxRetries = Math.Max(0, _options.CurrentValue.MaxSendRetries);
        Exception? lastError = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _sender.SendAsync(
                    job.Destination,
                    job.Text,
                    job.ParseMode,
                    job.DisableNotification,
                    cancellationToken);

                if (attempt > 0)
                {
                    _logger.LogInformation(
                        "Delivered notification {JobId} on attempt {Attempt}.",
                        job.Id, attempt + 1);
                }
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Service shutting down — don't burn retries on a doomed call.
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;

                if (attempt < maxRetries)
                {
                    // Linear-ish backoff capped at ~5s. Telegram's rate-limit
                    // window is short enough that exponential backoff buys
                    // little here, and we don't want to delay the next job
                    // in line by minutes.
                    var delay = TimeSpan.FromMilliseconds(500 * (attempt + 1));
                    _logger.LogWarning(ex,
                        "Notification {JobId} attempt {Attempt} failed; retrying in {Delay}ms.",
                        job.Id, attempt + 1, delay.TotalMilliseconds);
                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                }
            }
        }

        // All retries exhausted — log and move on. The next job in the
        // queue must not be blocked by a single bad destination.
        _logger.LogError(lastError,
            "Dropping notification {JobId} (client {Client}, destination {Destination}) after {Attempts} attempts.",
            job.Id, job.ClientName, job.DestinationName, maxRetries + 1);
    }
}
