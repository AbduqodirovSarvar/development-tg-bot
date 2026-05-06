namespace DevelopmentTgBot.Configuration;

/// <summary>
/// One configured API key + the destinations it's allowed to write to.
/// Allowed-destination patterns support a trailing wildcard (e.g.
/// <c>familytree.*</c> matches any destination whose name starts with
/// <c>familytree.</c>). A single <c>*</c> grants access to everything —
/// reserve that for an admin/operator key.
///
/// <para>The list is matched on every request; keep it short. For
/// large fleets, swap this for a database lookup.</para>
/// </summary>
public sealed class ApiClientOptions
{
    /// <summary>Display name used in logs (e.g. "FamilyTree-prod").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human-readable note about what this key is for. Surfaced in logs only.</summary>
    public string? Description { get; set; }

    /// <summary>The bearer/X-Api-Key value the client must send.</summary>
    public string Key { get; set; } = string.Empty;

    public List<string> AllowedDestinations { get; set; } = new();
}
