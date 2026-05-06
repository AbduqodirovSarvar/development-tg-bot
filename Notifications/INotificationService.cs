using System.Security.Claims;
using DevelopmentTgBot.Contracts;

namespace DevelopmentTgBot.Notifications;

public interface INotificationService
{
    /// <summary>
    /// Validates the request against the caller's allowed destinations,
    /// resolves the destination name to chat/topic IDs, and enqueues the
    /// job for the dispatcher. Throws <see cref="NotificationException"/>
    /// for client-visible errors (unknown destination, forbidden,
    /// queue full); the endpoint maps those to HTTP status codes.
    /// </summary>
    SendNotificationResponse Enqueue(SendNotificationRequest request, ClaimsPrincipal caller);
}
