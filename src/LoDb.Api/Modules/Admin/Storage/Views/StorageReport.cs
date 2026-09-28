namespace LoDb.Api.Modules.Admin.Storage.Views;

/// <summary>
/// What the storage root holds: its objects by family, the images and their WebP
/// siblings, the datasets by version, language and type, and how much the content
/// addressing saves.
/// </summary>
internal sealed record StorageReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>False when the root could not be read; every figure is then empty.</summary>
    public required bool Ok { get; init; }

    /// <summary>Why the root could not be read; null when it could.</summary>
    public string? Error { get; init; }

    public required long Objects { get; init; }

    public required long Bytes { get; init; }

    /// <summary>blobs, data and other, the heaviest first.</summary>
    public required IReadOnlyList<StorageRow> Families { get; init; }

    public required BlobFigures Blobs { get; init; }

    public required DatasetFigures Data { get; init; }

    public required DedupFigures Dedup { get; init; }

    /// <summary>The heaviest objects, the heaviest first.</summary>
    public required IReadOnlyList<StoredObject> Largest { get; init; }

    /// <summary>Objects written by UTC day, oldest first.</summary>
    public required IReadOnlyList<StorageDay> Timeline { get; init; }

    /// <summary>The languages and types of each version, the latest version first.</summary>
    public required IReadOnlyList<VersionCoverage> Coverage { get; init; }
}
