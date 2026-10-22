using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Building;

/// <summary>What indexing a version writes: the states it closes, then those it opens.</summary>
/// <remarks>The states it extends are written nowhere: an open state has no last version.</remarks>
internal sealed record VersionChanges
{
    public required PatchVersion Version { get; init; }

    public required IReadOnlyList<ClosedState> Closed { get; init; }

    public required IReadOnlyList<OpenedState> Opened { get; init; }
}
