using System.Collections.Concurrent;

namespace LoDb.Ingestion.Images;

/// <summary>
/// The counts of one image batch, kept by its parallel fetches, then read once as its
/// <see cref="ImageBatchReport"/>.
/// </summary>
internal sealed class ImageBatchTally
{
    // Files a refusal line names, of a batch that may refuse hundreds.
    private const int RefusedSampleSize = 5;

    private readonly ConcurrentQueue<string> refusedSample = new();
    private int stored;
    private int absent;
    private int refused;
    private int failed;
    private int blobs;
    private int webp;

    public void CountAbsent() => Interlocked.Increment(ref absent);

    /// <summary>Counts a refused file, and keeps its name among the first ones.</summary>
    public void CountRefused(string file)
    {
        if (Interlocked.Increment(ref refused) <= RefusedSampleSize)
        {
            refusedSample.Enqueue(file);
        }
    }

    public void CountFailed() => Interlocked.Increment(ref failed);

    /// <summary>Counts a stored image, and the files its storing wrote.</summary>
    public void CountStored(bool blobWritten, bool webpWritten)
    {
        Interlocked.Increment(ref stored);
        if (blobWritten)
        {
            Interlocked.Increment(ref blobs);
        }

        if (webpWritten)
        {
            Interlocked.Increment(ref webp);
        }
    }

    public ImageBatchReport Report(int requested, int skipped) => new()
    {
        Requested = requested,
        Skipped = skipped,
        Stored = Volatile.Read(ref stored),
        Absent = Volatile.Read(ref absent),
        Refused = Volatile.Read(ref refused),
        RefusedSample = [.. refusedSample],
        Failed = Volatile.Read(ref failed),
        BlobsWritten = Volatile.Read(ref blobs),
        WebpWritten = Volatile.Read(ref webp),
    };
}
