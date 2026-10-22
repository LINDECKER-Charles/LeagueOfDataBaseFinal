namespace LoDb.Api.Modules.Catalog.Changes;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/changes</c>: what a patch changed against the version
/// listed before it: the entities whose state opens there and those it retired.
/// </summary>
/// <remarks>
/// Exists for every covered version but the oldest one: any other answers 404
/// (<c>no-patch-page</c>).
/// </remarks>
internal sealed record PatchChanges
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    /// <summary>The version compared to: the covered version before it.</summary>
    public required string PreviousVersion { get; init; }

    /// <summary>
    /// Whether the patch changed anything; a page without is <c>noindex, follow</c>, and
    /// out of the sitemaps.
    /// </summary>
    public required bool HasChanges { get; init; }

    /// <summary>One group per resource that changed, in the order of the resources.</summary>
    public required IReadOnlyList<PatchChangeGroup> Groups { get; init; }
}
