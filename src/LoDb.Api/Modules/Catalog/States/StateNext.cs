using LoDb.Api.Modules.Catalog.Shared;

namespace LoDb.Api.Modules.Catalog.States;

/// <summary>
/// What followed a closed state (« Changé au patch X », « Retiré au patch X »), on a frozen
/// page only.
/// </summary>
internal sealed record StateNext
{
    public required StateNextKind Kind { get; init; }

    /// <summary>The version that changed or no longer holds the entity.</summary>
    public required string Version { get; init; }

    /// <summary>
    /// The next opener's versioned URL for a change (a 301 while that version is the latest,
    /// accepted: a frozen page never changes); null for a removal.
    /// </summary>
    public required CanonicalLink? Link { get; init; }
}
