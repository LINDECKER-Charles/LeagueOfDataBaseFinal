using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;

namespace LoDb.Ingestion.Catalog.Snapshots;

/// <summary>An entry a search found.</summary>
public sealed record SearchHit
{
    public required ResourceType Type { get; init; }

    /// <summary>The id the resource's <see cref="CatalogResource{TEntry}.Find"/> takes.</summary>
    public required string Id { get; init; }

    /// <summary>Display name in the catalog's language.</summary>
    public required string Name { get; init; }

    public required CanonicalPath Path { get; init; }
}
