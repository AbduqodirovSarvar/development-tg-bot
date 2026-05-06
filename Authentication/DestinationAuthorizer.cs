using System.Security.Claims;

namespace DevelopmentTgBot.Authentication;

/// <summary>
/// Pattern-matches a requested destination name against the authenticated
/// client's allowed-destination claims. Patterns support a single trailing
/// <c>*</c> wildcard (e.g. <c>familytree.*</c> matches <c>familytree.bugs</c>
/// but not <c>shopapp.bugs</c>). A bare <c>*</c> matches everything and
/// is meant for an admin/operator key.
///
/// <para>This is a pure function — no I/O, no DI surface — so the endpoint
/// can call it inline without bringing in another service.</para>
/// </summary>
public static class DestinationAuthorizer
{
    public static bool IsAllowed(ClaimsPrincipal principal, string destination)
    {
        var patterns = principal.FindAll(ApiKeyClaimTypes.AllowedDestination)
                                .Select(c => c.Value);

        foreach (var pattern in patterns)
        {
            if (Matches(pattern, destination))
                return true;
        }

        return false;
    }

    private static bool Matches(string pattern, string destination)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        if (pattern == "*")
            return true;

        if (pattern.EndsWith('*'))
        {
            // Trailing-wildcard form: prefix match. The prefix length excludes
            // the '*', so "familytree.*" requires destination to literally
            // start with "familytree." — we don't accept "familytreeXbugs".
            var prefix = pattern[..^1];
            return destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        // No wildcard — require an exact match (case-insensitive to mirror
        // the destination dictionary lookup).
        return string.Equals(pattern, destination, StringComparison.OrdinalIgnoreCase);
    }
}
