namespace DevelopmentTgBot.Authentication;

/// <summary>
/// Custom claim types stamped on the authenticated principal so the
/// endpoint layer can authorize destinations without re-reading config.
/// </summary>
public static class ApiKeyClaimTypes
{
    public const string ClientName = "client_name";

    /// <summary>
    /// One claim per allowed-destination pattern (e.g. <c>familytree.*</c>).
    /// Multiple claims with the same type are read with
    /// <c>FindAll(...).Select(c => c.Value)</c>.
    /// </summary>
    public const string AllowedDestination = "allowed_destination";
}
