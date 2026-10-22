using LoDb.Domain.Catalog;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Building;

/// <summary>
/// A state a version closes, by a change of the entity or its retirement: what identifies it,
/// and the covered version before the one that closes it, its last.
/// </summary>
internal sealed record ClosedState
{
    public required ResourceType Resource { get; init; }

    public required string Key { get; init; }

    public required PatchVersion Opener { get; init; }

    public required PatchVersion Last { get; init; }

    public required string Fingerprint { get; init; }
}
