using LoDb.Domain.Versions;
using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Pipeline.Versions;

/// <summary>What an ingestion run of a version did.</summary>
public sealed record VersionIngestionResult
{
    public required PatchVersion Version { get; init; }

    public required VersionIngestionOutcome Outcome { get; init; }

    /// <summary>Datasets written by this run; stored ones are skipped.</summary>
    public int DatasetsWritten { get; init; }

    /// <summary>The image batch, when the run got that far.</summary>
    public ImageBatchReport? Images { get; init; }

    /// <summary>Whether this run made the version the latest one.</summary>
    public bool Promoted { get; init; }
}
