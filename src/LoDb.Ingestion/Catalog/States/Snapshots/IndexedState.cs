using LoDb.Domain.Catalog;
using LoDb.Domain.States;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Snapshots;

/// <summary>
/// A state of an entity: the run of covered versions over which its en_US displayed
/// projection keeps one fingerprint. Its first version, the opener, is the page indexed.
/// </summary>
public sealed record IndexedState
{
    public required ResourceType Resource { get; init; }

    /// <summary>The entity's folded id (<c>EntityKey</c>).</summary>
    public required string Key { get; init; }

    public required PatchVersion Opener { get; init; }

    /// <summary>
    /// The last version of a closed state; null while open, whose last version is the coverage
    /// end (never named on a page, so that a promotion changes nothing of it).
    /// </summary>
    public PatchVersion? Last { get; init; }

    /// <summary>When the state was closed: an opener's <c>lastmod</c>. Null while open.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    public required string Fingerprint { get; init; }

    /// <summary>
    /// The entity's canonical path in the opener version (<c>items/3371-molten-edge</c>), with
    /// the slug and the case of the id there.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>The opener of the entity's previous state; null for a new entity.</summary>
    public PatchVersion? PreviousOpener { get; init; }

    /// <summary>Whether the entity comes back after versions without it.</summary>
    public bool IsReturn { get; init; }

    /// <summary>The en_US fields that differ from the previous state's last version.</summary>
    public required IReadOnlyList<FieldKey> ChangedFields { get; init; }

    public bool IsOpen => Last is null;

    public EntityChangeKind Kind =>
        PreviousOpener is null ? EntityChangeKind.New
        : IsReturn ? EntityChangeKind.Returned
        : EntityChangeKind.Changed;

    /// <summary>
    /// Whether <paramref name="version"/> lies in the state: from its opener to its last
    /// version, or onwards while it is open.
    /// </summary>
    public bool Holds(PatchVersion version) =>
        Opener <= version && (Last is null || version <= Last);
}
