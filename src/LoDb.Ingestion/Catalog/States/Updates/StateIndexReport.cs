using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Updates;

/// <summary>What an update of the index of states did, and the coverage it left.</summary>
public sealed record StateIndexReport
{
    public required StateIndexOutcome Outcome { get; init; }

    /// <summary>The current generation after the update; null while none is.</summary>
    public long? GenerationId { get; init; }

    /// <summary>The oldest covered version; null while none is.</summary>
    public PatchVersion? CoverageStart { get; init; }

    /// <summary>The newest covered version; null while none is.</summary>
    public PatchVersion? CoverageEnd { get; init; }

    /// <summary>The versions this update indexed.</summary>
    public int VersionsIndexed { get; init; }
}
