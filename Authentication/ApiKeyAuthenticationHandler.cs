using System.Security.Claims;
using System.Text.Encodings.Web;
using DevelopmentTgBot.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DevelopmentTgBot.Authentication;

/// <summary>
/// Validates either <c>Authorization: Bearer &lt;key&gt;</c> or the
/// shorthand <c>X-Api-Key: &lt;key&gt;</c>. Both are accepted because
/// shell scripts (CI/CD <c>curl</c>) often default to one or the other,
/// and there's no security reason to force a specific header for an
/// already-opaque token.
///
/// <para>Successful authentication stamps the client's name and its
/// allowed-destination patterns onto the <see cref="ClaimsPrincipal"/> so
/// the endpoint can authorize without another config lookup.</para>
/// </summary>
public sealed class ApiKeyAuthenticationHandler
    : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeader = "X-Api-Key";
    private const string BearerPrefix = "Bearer ";

    private readonly IOptionsMonitor<GatewayOptions> _gateway;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<GatewayOptions> gateway)
        : base(options, logger, encoder)
    {
        _gateway = gateway;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var providedKey = ReadKeyFromHeaders();
        if (string.IsNullOrEmpty(providedKey))
        {
            // No header at all — let the next scheme (or the [Authorize]
            // challenge) decide. NoResult is the correct signal here, not
            // Fail: failure short-circuits the auth pipeline.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var clients = _gateway.CurrentValue.ApiClients;
        // Linear scan — the configured client list is small (single digits
        // in practice). Swap for a dictionary if it ever grows past that.
        var match = clients.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.Key) &&
            string.Equals(c.Key, providedKey, StringComparison.Ordinal));

        if (match is null)
        {
            Logger.LogWarning("Rejected request with unrecognized API key (length {Len}).", providedKey.Length);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, match.Name),
            new(ApiKeyClaimTypes.ClientName, match.Name)
        };
        foreach (var pattern in match.AllowedDestinations)
        {
            claims.Add(new Claim(ApiKeyClaimTypes.AllowedDestination, pattern));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string? ReadKeyFromHeaders()
    {
        if (Request.Headers.TryGetValue("Authorization", out var auth))
        {
            var raw = auth.ToString();
            if (raw.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
                return raw[BearerPrefix.Length..].Trim();
        }

        if (Request.Headers.TryGetValue(ApiKeyHeader, out var header))
            return header.ToString().Trim();

        return null;
    }
}
