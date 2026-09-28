using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Infrastructure.Storage.Blobs;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Images;

/// <summary>
/// Fetches images, stores each one content-addressed with its WebP sibling, and records its
/// verdict in the manifest: present, or absent after a 403/404.
/// </summary>
/// <remarks>
/// A transient failure records nothing, so that the next run asks again. Fetches run in
/// parallel up to <c>LoDb:Egress:FetchConcurrency</c>; verdicts are recorded by chunks of
/// <c>RecordBatchSize</c>, so a run cut short keeps what it settled. One summary line per
/// batch.
/// </remarks>
internal sealed partial class ImageIngestion(
    IEgressFetcher fetcher,
    IBlobStore blobs,
    IDdragonAssetStore assets,
    ImageManifest manifest,
    IngestionMetrics metrics,
    IOptions<EgressOptions> egress,
    IOptions<IngestionOptions> options,
    TimeProvider timeProvider,
    ILogger<ImageIngestion> logger)
{
    // The legacy default for a file name without an extension.
    private const string DefaultExtension = WebpSibling.SourceExtension;

    public async Task<ImageBatchReport> IngestAsync(
        ImageBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var started = timeProvider.GetTimestamp();
        List<DdragonImage> images =
            [.. batch.Images.DistinctBy(static image => (image.ManifestType, image.File))];
        var pending = batch.Force
            ? images
            : await manifest.UnrecordedAsync(batch.Version, images, cancellationToken)
                .ConfigureAwait(false);
        if (pending.Count == 0)
        {
            return ImageBatchReport.Settled(images.Count);
        }

        var tally = await SettlePendingAsync(batch, pending, cancellationToken)
            .ConfigureAwait(false);
        var report = tally.Report(images.Count, images.Count - pending.Count);
        Publish(batch, report, timeProvider.GetElapsedTime(started));
        return report;
    }

    // The storage accepts [a-z0-9]{1,10}, the manifest [a-z0-9]{1,8}: the stricter wins.
    internal static string ExtensionOf(string file)
    {
        var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
        return BlobExtension().IsMatch(extension) ? extension : DefaultExtension;
    }

    private async Task<Tally> SettlePendingAsync(
        ImageBatch batch,
        List<DdragonImage> pending,
        CancellationToken cancellationToken)
    {
        var tally = new Tally();
        using var egressBatch = fetcher.OpenBatch();
        var run = new Run(batch, egressBatch, tally);
        foreach (var chunk in pending.Chunk(options.Value.RecordBatchSize))
        {
            await SettleChunkAsync(run, chunk, cancellationToken).ConfigureAwait(false);
        }

        return tally;
    }

    private async Task SettleChunkAsync(
        Run run,
        DdragonImage[] chunk,
        CancellationToken cancellationToken)
    {
        var entries = new ConcurrentQueue<DdragonAssetEntry>();
        var parallelism = new ParallelOptions
        {
            MaxDegreeOfParallelism = egress.Value.FetchConcurrency,
            CancellationToken = cancellationToken,
        };
        await Parallel.ForEachAsync(
                chunk,
                parallelism,
                async (image, token) =>
                {
                    if (await SettleAsync(run, image, token).ConfigureAwait(false) is { } entry)
                    {
                        entries.Enqueue(entry);
                    }

                    run.Batch.Progress?.Report(image);
                })
            .ConfigureAwait(false);
        await assets.RecordAsync(entries, run.Batch.Force, cancellationToken)
            .ConfigureAwait(false);
    }

    // Null when the image got no verdict: the transient failure is counted, never recorded.
    private async Task<DdragonAssetEntry?> SettleAsync(
        Run run,
        DdragonImage image,
        CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await run.Egress
                .FetchAsync(image.Url(run.Batch.Version), cancellationToken)
                .ConfigureAwait(false);
            if (outcome is not FetchOutcome.Present present)
            {
                return Absent(run, image);
            }

            var stored = await StoreAsync(present, ExtensionOf(image.File), cancellationToken)
                .ConfigureAwait(false);
            run.Tally.CountStored(stored);
            return Present(run, image, stored.Key);
        }
        catch (EgressException)
        {
            run.Tally.CountFailed();
            return null;
        }
    }

    private static DdragonAssetEntry Present(Run run, DdragonImage image, BlobKey key) =>
        DdragonAssetEntry.Present(
            run.Batch.Version.Value,
            image.ManifestType,
            image.File,
            key.Sha256,
            key.Extension);

    private DdragonAssetEntry Absent(Run run, DdragonImage image)
    {
        run.Tally.CountAbsent();
        metrics.RecordAbsence(image.ManifestType);
        return DdragonAssetEntry.Absent(run.Batch.Version.Value, image.ManifestType, image.File);
    }

    private async Task<StoredImage> StoreAsync(
        FetchOutcome.Present present,
        string extension,
        CancellationToken cancellationToken)
    {
        byte[] content;
        await using (present.Content.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await present.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            content = buffer.ToArray();
        }

        var stored = await blobs.StoreAsync(content, extension, cancellationToken)
            .ConfigureAwait(false);
        var webp = await EnsureWebpAsync(stored.Key, content, cancellationToken)
            .ConfigureAwait(false);
        return new StoredImage(stored.Key, stored.Written, webp);
    }

    // Once per blob: the sibling is shared by every key whose image has the same bytes.
    private async Task<bool> EnsureWebpAsync(
        BlobKey key,
        byte[] content,
        CancellationToken cancellationToken)
    {
        if (!key.HasWebpSlot
            || await blobs.HasWebpSiblingAsync(key, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return WebpTranscoder.ToWebp(content) is { } webp
            && await blobs.StoreWebpSiblingAsync(key, webp, cancellationToken)
                .ConfigureAwait(false);
    }

    private void Publish(ImageBatch batch, ImageBatchReport report, TimeSpan elapsed)
    {
        metrics.RecordDuration(IngestionMetrics.ImagesStage, elapsed);
        metrics.RecordBlobs(report.BlobsWritten, report.WebpWritten);
        metrics.RecordFetchFailures(report.Failed);
        var settled = report.Stored + report.Absent;
        if (report.IsComplete)
        {
            IngestionLog.ImagesCompleted(
                logger,
                batch.Version.Value,
                (long)elapsed.TotalMilliseconds,
                settled,
                report.Stored,
                report.Absent,
                report.BlobsWritten,
                report.WebpWritten);
            return;
        }

        IngestionLog.ImagesIncomplete(logger, batch.Version.Value, settled, report.Failed);
    }

    [GeneratedRegex("^[a-z0-9]{1,8}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex BlobExtension();

    private sealed record Run(ImageBatch Batch, IEgressBatch Egress, Tally Tally);

    private sealed record StoredImage(BlobKey Key, bool BlobWritten, bool WebpWritten);

    // Counted from the parallel fetches of one batch.
    private sealed class Tally
    {
        private int stored;
        private int absent;
        private int failed;
        private int blobs;
        private int webp;

        public void CountAbsent() => Interlocked.Increment(ref absent);

        public void CountFailed() => Interlocked.Increment(ref failed);

        public void CountStored(StoredImage image)
        {
            Interlocked.Increment(ref stored);
            if (image.BlobWritten)
            {
                Interlocked.Increment(ref blobs);
            }

            if (image.WebpWritten)
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
            Failed = Volatile.Read(ref failed),
            BlobsWritten = Volatile.Read(ref blobs),
            WebpWritten = Volatile.Read(ref webp),
        };
    }
}
