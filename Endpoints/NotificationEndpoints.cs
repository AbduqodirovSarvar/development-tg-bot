using DevelopmentTgBot.Authentication;
using DevelopmentTgBot.Configuration;
using DevelopmentTgBot.Contracts;
using DevelopmentTgBot.Notifications;
using DevelopmentTgBot.Telegram;
using Microsoft.Extensions.Options;

namespace DevelopmentTgBot.Endpoints;

/// <summary>
/// Minimal-API route surface. The endpoints here intentionally do little
/// beyond translating between HTTP and the application services — request
/// validation lives in DataAnnotations on the DTO, business rules live
/// in <see cref="NotificationService"/>, and Telegram I/O lives in the
/// dispatcher. That layering keeps each piece independently testable.
/// </summary>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notify").WithTags("Notifications");

        group.MapPost("/", SendNotification)
             .RequireAuthorization()
             .WithName("SendNotification")
             .WithSummary("Queue a Telegram message for delivery to a named destination.");

        // Documents skip the queue and are sent synchronously — buffering a
        // 50MB backup in the in-memory queue would dwarf typical text-message
        // memory use, and clients (DB-dump scripts, admin UI uploads) are
        // happy to wait for the upload to finish.
        group.MapPost("/document", SendDocument)
             .RequireAuthorization()
             .DisableAntiforgery()
             .WithName("SendDocument")
             .WithSummary("Upload a file to Telegram as a document. Synchronous; awaits Bot API response.");

        // Health endpoint left anonymous on purpose so liveness probes
        // don't need to ship an API key. Mounted at /healthz to mirror
        // the de-facto k8s convention.
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
           .WithTags("Infrastructure")
           .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> SendDocument(
        HttpContext context,
        IFormFile file,
        IOptionsMonitor<GatewayOptions> gatewayOptions,
        ITelegramSender telegramSender,
        ILoggerFactory loggerFactory,
        string? destination = null,
        string? caption = null,
        bool? disableNotification = null,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger("DocumentEndpoint");

        if (file is null || file.Length == 0)
            return Results.BadRequest(new ErrorResponse("validation_failed", "File is required."));

        if (string.IsNullOrWhiteSpace(destination))
            return Results.BadRequest(new ErrorResponse("validation_failed", "'destination' is required."));

        var gateway = gatewayOptions.CurrentValue;

        if (!gateway.Destinations.TryGetValue(destination, out var resolved))
            return Results.NotFound(new ErrorResponse(
                "unknown_destination",
                $"Destination '{destination}' is not configured."));

        if (!DestinationAuthorizer.IsAllowed(context.User, destination))
            return Results.Json(
                new ErrorResponse("forbidden_destination",
                    $"This API key is not allowed to write to '{destination}'."),
                statusCode: StatusCodes.Status403Forbidden);

        var clientName = context.User.FindFirst(ApiKeyClaimTypes.ClientName)?.Value ?? "(unnamed)";
        logger.LogInformation(
            "Forwarding document {FileName} ({Size} bytes) from {Client} to {Destination}.",
            file.FileName, file.Length, clientName, destination);

        try
        {
            // Stream the form-file body straight into TelegramSender — no
            // intermediate copy. The IFormFile abstraction already buffers
            // to disk for large uploads, so memory pressure stays bounded
            // regardless of how big the dump gets.
            await using var stream = file.OpenReadStream();
            await telegramSender.SendDocumentAsync(
                resolved,
                stream,
                file.FileName,
                caption,
                disableNotification,
                cancellationToken);

            return Results.Accepted(value: new { destination, fileName = file.FileName, sizeBytes = file.Length });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Document upload to {Destination} failed for client {Client}.", destination, clientName);
            return Results.Problem(
                detail: ex.Message,
                title: "Document upload failed.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static IResult SendNotification(
        SendNotificationRequest request,
        HttpContext context,
        INotificationService notifications)
    {
        // [Required] / [MaxLength] live on the DTO but minimal API doesn't
        // run the validation by default — surface obvious empties here so
        // we don't waste a queue slot. Keep this tight: deeper validation
        // is the service's job.
        if (string.IsNullOrWhiteSpace(request.Destination) || string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new ErrorResponse(
                "validation_failed",
                "Both 'destination' and 'text' are required."));
        }

        try
        {
            var response = notifications.Enqueue(request, context.User);
            // 202 Accepted (not 200) — we've queued the message, not
            // confirmed delivery. The semantics matter: a 200 here would
            // imply Telegram has it, which we can't promise from inside
            // the request thread.
            return Results.Accepted($"/api/notify/{response.Id}", response);
        }
        catch (NotificationException ex)
        {
            return ex.Reason switch
            {
                NotificationFailureReason.UnknownDestination =>
                    Results.NotFound(new ErrorResponse("unknown_destination", ex.Message)),

                NotificationFailureReason.ForbiddenDestination =>
                    Results.Json(
                        new ErrorResponse("forbidden_destination", ex.Message),
                        statusCode: StatusCodes.Status403Forbidden),

                NotificationFailureReason.QueueFull =>
                    Results.Json(
                        new ErrorResponse("queue_full", ex.Message),
                        statusCode: StatusCodes.Status503ServiceUnavailable),

                _ => Results.Problem(ex.Message)
            };
        }
    }
}
