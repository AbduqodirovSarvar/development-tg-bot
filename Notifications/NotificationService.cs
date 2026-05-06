using System.Security.Claims;
using DevelopmentTgBot.Authentication;
using DevelopmentTgBot.Configuration;
using DevelopmentTgBot.Contracts;
using Microsoft.Extensions.Options;

namespace DevelopmentTgBot.Notifications;

/// <summary>
/// Endpoint-side glue: looks the destination name up in config, checks
/// the caller's permissions, and hands the resolved job to the queue.
/// All Telegram I/O happens later in the dispatcher — this method must
/// stay fast because it runs inside the request thread.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IOptionsMonitor<GatewayOptions> _gateway;
    private readonly NotificationQueue _queue;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IOptionsMonitor<GatewayOptions> gateway,
        NotificationQueue queue,
        ILogger<NotificationService> logger)
    {
        _gateway = gateway;
        _queue = queue;
        _logger = logger;
    }

    public SendNotificationResponse Enqueue(SendNotificationRequest request, ClaimsPrincipal caller)
    {
        var gateway = _gateway.CurrentValue;

        if (!gateway.Destinations.TryGetValue(request.Destination, out var destination))
        {
            // 404 rather than 403 here — telling the client "that name
            // doesn't exist" is fine; they can't probe for hidden names
            // because the client list is configured by the operator.
            throw new NotificationException(
                NotificationFailureReason.UnknownDestination,
                $"Destination '{request.Destination}' is not configured.");
        }

        if (!DestinationAuthorizer.IsAllowed(caller, request.Destination))
        {
            // The destination exists but this key isn't on its allow-list.
            // 403 + clear message — leaking the destination's existence is
            // OK because the caller already knew the name.
            throw new NotificationException(
                NotificationFailureReason.ForbiddenDestination,
                $"This API key is not allowed to write to '{request.Destination}'.");
        }

        var clientName = caller.FindFirstValue(ApiKeyClaimTypes.ClientName) ?? "(unnamed)";
        var job = new NotificationJob(
            Id: Guid.NewGuid(),
            ClientName: clientName,
            DestinationName: request.Destination,
            Destination: destination,
            Text: request.Text,
            ParseMode: request.ParseMode,
            DisableNotification: request.DisableNotification);

        if (!_queue.TryEnqueue(job))
        {
            // Queue is at capacity — back-pressure signal. The caller
            // should retry with backoff or alert that we're saturated.
            throw new NotificationException(
                NotificationFailureReason.QueueFull,
                "Notification queue is full; retry shortly.");
        }

        _logger.LogInformation(
            "Queued notification {JobId} from {ClientName} to {Destination}.",
            job.Id, clientName, request.Destination);

        return new SendNotificationResponse(job.Id, request.Destination);
    }
}
