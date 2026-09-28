namespace LoDb.Api.Modules.Profiles.Identity;

/// <summary>Body of <c>PUT /api/profile/identity</c>: the summoner name shown everywhere.</summary>
internal sealed record IdentityRequest
{
    /// <summary>
    /// 3 to 24 letters, digits, <c>_</c>, <c>.</c> or <c>-</c>, starting with a letter or a
    /// digit; unique whatever its case.
    /// </summary>
    public required string? Username { get; init; }

    /// <summary>The Riot tag line, 3 to 5 letters or digits; null or blank for none.</summary>
    public required string? RiotTagline { get; init; }
}
