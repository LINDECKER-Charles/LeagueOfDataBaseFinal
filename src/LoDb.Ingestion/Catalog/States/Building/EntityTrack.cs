using LoDb.Domain.Catalog;
using LoDb.Domain.States.Projections;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Building;

/// <summary>
/// The last state of an entity while the index walks the versions: what the next version is
/// compared to.
/// </summary>
internal sealed class EntityTrack
{
    public required ResourceType Resource { get; init; }

    public required string Key { get; init; }

    /// <summary>The opener of the entity's last state.</summary>
    public required PatchVersion LastOpener { get; set; }

    /// <summary>The fingerprint of the entity's last state.</summary>
    public required string Fingerprint { get; set; }

    /// <summary>Whether the last state holds the version walked last (not closed).</summary>
    public required bool IsOpen { get; set; }

    /// <summary>
    /// The projection of the last state, the same at every version of it; null until read
    /// back from its opener, when the walk resumes from the stored states.
    /// </summary>
    public IReadOnlyList<ProjectedField>? Projection { get; set; }

    /// <summary>A track for an entity first seen at <paramref name="opener"/>.</summary>
    public static EntityTrack Opened(ProjectedEntity entity, PatchVersion opener)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new EntityTrack
        {
            Resource = entity.Resource,
            Key = entity.Key,
            LastOpener = opener,
            Fingerprint = entity.Fingerprint,
            IsOpen = true,
            Projection = entity.Projection,
        };
    }

    /// <summary>The entity opens a new state at <paramref name="opener"/>.</summary>
    public void Reopen(ProjectedEntity entity, PatchVersion opener)
    {
        ArgumentNullException.ThrowIfNull(entity);
        LastOpener = opener;
        Fingerprint = entity.Fingerprint;
        IsOpen = true;
        Projection = entity.Projection;
    }
}
