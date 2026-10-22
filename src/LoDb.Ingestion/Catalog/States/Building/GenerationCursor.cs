using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Building;

/// <summary>
/// Where a walk stands in a generation: the version written last, the position of the next
/// one in the coverage, the last state of every entity, and what the walk has done so far.
/// </summary>
internal sealed class GenerationCursor
{
    public required long GenerationId { get; init; }

    public required StateTracks Tracks { get; init; }

    public required ClosingDates Closing { get; init; }

    /// <summary>The covered version written last; null before the first one.</summary>
    public PatchVersion? Previous { get; set; }

    /// <summary>The ordinal of the next covered version (<c>catalog_state_version</c>).</summary>
    public int NextOrdinal { get; set; }

    /// <summary>The versions this walk indexed.</summary>
    public int Indexed { get; private set; }

    /// <summary>The states this walk opened.</summary>
    public int Opened { get; private set; }

    /// <summary>The states this walk closed.</summary>
    public int Closed { get; private set; }

    /// <summary>Moves past a version once its changes are written.</summary>
    public void Advance(VersionChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Previous = changes.Version;
        NextOrdinal++;
        Indexed++;
        Opened += changes.Opened.Count;
        Closed += changes.Closed.Count;
    }
}
