namespace LoDb.Ingestion.Images;

/// <summary>
/// What one image batch did: the counts of its summary line.
/// </summary>
public sealed record ImageBatchReport
{
    /// <summary>Distinct manifest keys asked for.</summary>
    public required int Requested { get; init; }

    /// <summary>Keys the manifest already settled, not fetched again.</summary>
    public required int Skipped { get; init; }

    /// <summary>Keys recorded present by this batch.</summary>
    public required int Stored { get; init; }

    /// <summary>Keys recorded absent by this batch (403/404, never fetched again).</summary>
    public required int Absent { get; init; }

    /// <summary>
    /// Keys left without a verdict after a transient failure: nothing recorded, the next run
    /// tries them again.
    /// </summary>
    public required int Failed { get; init; }

    /// <summary>New blobs: an image already stored under another key costs no write.</summary>
    public required int BlobsWritten { get; init; }

    /// <summary>New WebP siblings.</summary>
    public required int WebpWritten { get; init; }

    /// <summary>Every key has a verdict.</summary>
    public bool IsComplete => Failed == 0;

    /// <summary>Nothing to fetch: every key was already settled.</summary>
    public static ImageBatchReport Settled(int requested) => new()
    {
        Requested = requested,
        Skipped = requested,
        Stored = 0,
        Absent = 0,
        Failed = 0,
        BlobsWritten = 0,
        WebpWritten = 0,
    };
}
