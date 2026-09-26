namespace LoDb.Api.Modules.Profiles.Curation;

/// <summary>Body of <c>PUT /api/profile/visibility</c>.</summary>
internal sealed record VisibilityRequest
{
    /// <summary>Whether visitors see the public card; a private one answers them 404.</summary>
    public required bool IsPublic { get; init; }
}
