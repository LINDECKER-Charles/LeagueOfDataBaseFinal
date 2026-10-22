using LoDb.Domain.Catalog;
using LoDb.Domain.States.Projections;

namespace LoDb.Ingestion.Catalog.States.Building;

/// <summary>An entity of a version as the index compares it: its en_US projection.</summary>
internal sealed record ProjectedEntity
{
    public required ResourceType Resource { get; init; }

    /// <summary>The folded id (<c>EntityKey</c>).</summary>
    public required string Key { get; init; }

    /// <summary>Its canonical path in this version, the case of the id there kept.</summary>
    public required string Path { get; init; }

    public required IReadOnlyList<ProjectedField> Projection { get; init; }

    public required string Fingerprint { get; init; }
}
