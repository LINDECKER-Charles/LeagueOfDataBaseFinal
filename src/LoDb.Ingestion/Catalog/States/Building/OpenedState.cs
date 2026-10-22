using LoDb.Domain.Catalog;
using LoDb.Domain.States;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Building;

/// <summary>A state a version opens: a new entity, a changed one, or one come back.</summary>
internal sealed record OpenedState
{
    public required ResourceType Resource { get; init; }

    public required string Key { get; init; }

    public required PatchVersion Opener { get; init; }

    public required string Fingerprint { get; init; }

    /// <summary>The entity's canonical path in the opener version.</summary>
    public required string Path { get; init; }

    /// <summary>The opener of the entity's previous state; null for a new entity.</summary>
    public PatchVersion? PreviousOpener { get; init; }

    public required EntityChangeKind Kind { get; init; }

    /// <summary>
    /// The en_US fields that differ from the previous state, whose projection is the same at
    /// every version of it; none for a new entity, and maybe none for a return.
    /// </summary>
    public required IReadOnlyList<FieldKey> ChangedFields { get; init; }
}
