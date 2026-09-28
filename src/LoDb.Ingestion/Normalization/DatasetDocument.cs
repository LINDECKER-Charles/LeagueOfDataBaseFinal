namespace LoDb.Ingestion.Normalization;

/// <summary>
/// A normalized dataset as stored under <c>data/{version}/{lang}/{type}.json</c>.
/// </summary>
/// <remarks>
/// Entries keep the upstream order. An empty dataset with no <see cref="ContentLanguage"/> is
/// a definitive absence (neither the language nor <c>en_US</c> ships the file, UP 1): it is
/// persisted like any other so that nothing asks the upstream again.
/// </remarks>
public sealed record DatasetDocument<TEntry>
{
    /// <summary>The version, as Data Dragon writes it ("16.19.1").</summary>
    public required string Version { get; init; }

    /// <summary>The requested language, the one the dataset is stored under.</summary>
    public required string Language { get; init; }

    /// <summary>
    /// The language the entries are written in: <see cref="Language"/>, <c>en_US</c> after a
    /// fallback (UP 2), or <see langword="null"/> when no language ships the file.
    /// </summary>
    public string? ContentLanguage { get; init; }

    public required IReadOnlyList<TEntry> Entries { get; init; }
}
