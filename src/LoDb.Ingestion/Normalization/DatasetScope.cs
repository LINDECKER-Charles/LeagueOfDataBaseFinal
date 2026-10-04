using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Storage.Datasets;

namespace LoDb.Ingestion.Normalization;

/// <summary>
/// The (version, language) pair a dataset is requested for.
/// </summary>
public sealed record DatasetScope
{
    public required PatchVersion Version { get; init; }

    /// <summary>The requested language, which names the dataset even when it falls back.</summary>
    public required DdragonLanguage Language { get; init; }

    /// <summary>Where the pipeline stores the dataset of <paramref name="type"/>.</summary>
    public DatasetKey Key<TEntry>(DatasetType<TEntry> type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return new DatasetKey(Version.Value, Language.Code, type.Name);
    }
}
