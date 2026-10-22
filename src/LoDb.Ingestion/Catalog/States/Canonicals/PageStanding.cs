using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Ingestion.Catalog.States.Canonicals;

/// <summary>
/// Where a page of an entity stands to the index of states, and the page search engines
/// index for it.
/// </summary>
public sealed record PageStanding
{
    public required IndexedEntity Entity { get; init; }

    /// <summary>The page's version.</summary>
    public required PatchVersion Version { get; init; }

    /// <summary>How the index reached the page's state, if it did.</summary>
    public required StateLocation Location { get; init; }

    /// <summary>Never null: the page itself when nothing else is indexed for it.</summary>
    public required CanonicalTarget Canonical { get; init; }

    /// <summary>Whether the page is at the latest version, under its short URL.</summary>
    public required bool IsLatest { get; init; }

    /// <summary>
    /// Whether the latest version holds the entity (« Dans le dernier patch »).
    /// </summary>
    public required bool IsPresentInLatest { get; init; }

    /// <summary>The state that holds the page; null outside the index.</summary>
    public IndexedState? State => Location.State;

    /// <summary>Whether the page's version opened its state.</summary>
    public bool IsOpener => State is { } state && state.Opener == Version;
}
