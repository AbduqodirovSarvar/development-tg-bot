using DevelopmentTgBot.Authentication;
using DevelopmentTgBot.Contracts;
using DevelopmentTgBot.Notifications;

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

        // Health endpoint left anonymous on purpose so liveness probes
        // don't need to ship an API key. Mounted at /healthz to mirror
        // the de-facto k8s convention.
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
           .WithTags("Infrastructure")
           .AllowAnonymous();

        return app;
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
