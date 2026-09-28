using LoDb.Domain.Languages;

namespace LoDb.Ingestion.Pipeline.Versions;

/// <summary>What one ingestion run of a version covers.</summary>
public sealed record IngestionRequest
{
    /// <summary>Every language Data Dragon lists; only what is missing is fetched.</summary>
    public static IngestionRequest Complete { get; } = new();

    /// <summary>
    /// The languages whose datasets are ingested, <see langword="null"/> for every language
    /// Data Dragon lists. Only a run over every listed language moves the version's state
    /// and may promote it.
    /// </summary>
    public IReadOnlyList<DdragonLanguage>? Languages { get; init; }

    /// <summary>
    /// Fetches every image again and replaces the manifest rows whose verdict changed. Stored
    /// datasets and blobs are written once and stay as they are.
    /// </summary>
    public bool Force { get; init; }
}
