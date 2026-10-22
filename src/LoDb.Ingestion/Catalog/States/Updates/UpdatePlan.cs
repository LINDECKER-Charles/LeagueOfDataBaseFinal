using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.States;

namespace LoDb.Ingestion.Catalog.States.Updates;

/// <summary>What an update found before writing anything.</summary>
internal sealed record UpdatePlan
{
    public required StateIndexRequest Request { get; init; }

    /// <summary>The current generation of the compiled scheme; null while none is.</summary>
    public CatalogStateGeneration? Current { get; init; }

    /// <summary>
    /// The coverage it aims at, oldest first: the run of listed versions whose en_US datasets
    /// are stored, ending at the latest one; empty before the first promotion.
    /// </summary>
    public required IReadOnlyList<PatchVersion> Run { get; init; }

    /// <summary>The versions whose en_US datasets were ingested to make the run.</summary>
    public int Ingested { get; init; }

    /// <summary>The versions of the run after <paramref name="version"/>; all for null.</summary>
    public IReadOnlyList<PatchVersion> After(PatchVersion? version) =>
        version is null ? Run : [.. Run.Where(covered => covered > version)];
}
