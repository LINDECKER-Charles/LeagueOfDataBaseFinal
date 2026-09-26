namespace LoDb.Api.Modules.Profiles.Curation;

/// <summary>Body of <c>PUT /api/profile/version</c>.</summary>
internal sealed record PreferredVersionRequest
{
    /// <summary>
    /// A version Data Dragon lists, such as 16.19.1; null or blank to follow the version
    /// browsed.
    /// </summary>
    public required string? Version { get; init; }
}
