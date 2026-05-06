using Microsoft.AspNetCore.Authentication;

namespace DevelopmentTgBot.Authentication;

/// <summary>
/// AuthenticationScheme options. Empty by design — the configured client
/// list is read from <see cref="Configuration.GatewayOptions.ApiClients"/>
/// at request time, not snapshotted into the scheme. This way a new key
/// added to appsettings becomes effective on the next config reload
/// without re-registering the scheme.
/// </summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";
}
