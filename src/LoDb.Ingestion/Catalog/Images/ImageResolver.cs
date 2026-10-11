using LoDb.Domain.Versions;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Catalog.Images;

/// <inheritdoc cref="IImageResolver"/>
internal sealed partial class ImageResolver(
    ImageManifest manifest,
    ImageVerdicts verdicts,
    IOnDemandIngestion onDemand,
    ILogger<ImageResolver> logger) : IImageResolver
{
    public async Task<ImageResolution> ResolveAsync(
        PatchVersion version,
        IReadOnlyCollection<DdragonImage> images,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(demand);
        List<DdragonImage> wanted = [.. images.DistinctBy(KeyOf)];
        var memo = verdicts.Of(version);
        await ReadAsync(new Batch(version, wanted, memo), demand, cancellationToken)
            .ConfigureAwait(false);
        List<DdragonImage> unknown =
            [.. wanted.Where(image => memo.Get(KeyOf(image)).Status == ImageStatus.Pending)];
        var refused = unknown.Count > 0
            && await SettleAsync(new Batch(version, unknown, memo), demand, cancellationToken)
                .ConfigureAwait(false);
        return new ImageResolution(
            wanted.ToDictionary(KeyOf, image => memo.Get(KeyOf(image))),
            refused);
    }

    private static (string Type, string File) KeyOf(DdragonImage image) =>
        (image.ManifestType, image.File);

    // One manifest query for the keys the memo cannot answer: never read, or without a row
    // when last read, which only a reader of what is stored accepts as it is.
    private async Task ReadAsync(
        Batch batch,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        var acceptsUnrecorded = demand.Effective == ColdPolicy.StoredOnly;
        List<DdragonImage> asked =
        [
            .. batch.Images.Where(image =>
                batch.Memo.Lookup(KeyOf(image), acceptsUnrecorded) is null),
        ];
        if (asked.Count == 0)
        {
            return;
        }

        var rows = await manifest.FindAsync(batch.Version, asked, cancellationToken)
            .ConfigureAwait(false);
        batch.Memo.Record(asked, rows);
    }

    // Records the verdicts it waited for into the memo; true when the work was refused.
    private async Task<bool> SettleAsync(
        Batch unsettled,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        switch (demand.Effective)
        {
            case ColdPolicy.Synchronous:
                return await WaitAsync(unsettled, cancellationToken).ConfigureAwait(false);
            case ColdPolicy.Queued:
                return !onDemand.TryEnqueue(new OnDemandRequest
                {
                    Version = unsettled.Version,
                    Images = unsettled.Images,
                    Origin = demand.Origin,
                });
            default:
                return false;
        }
    }

    // An upstream failure leaves the images pending: the page still renders, with placeholders.
    // A refusal (budget spent, no slot) leaves pending what it did not admit, and is told; the
    // verdicts of the images it admitted are read all the same.
    private async Task<bool> WaitAsync(Batch unsettled, CancellationToken cancellationToken)
    {
        var (version, unknown, memo) = unsettled;
        var refused = false;
        try
        {
            await onDemand.EnsureImagesAsync(version, unknown, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OnDemandRefusedException)
        {
            refused = true;
        }
        catch (EgressException exception)
        {
            LogImagesFailed(logger, version.Value, unknown.Count, exception);
            return false;
        }

        var settled = await manifest.FindAsync(version, unknown, cancellationToken)
            .ConfigureAwait(false);
        memo.Record(unknown, settled);
        return refused;
    }

    [LoggerMessage(
        EventName = "catalog.images.failed",
        Level = LogLevel.Warning,
        Message = "Images of version {Version} left pending: {Count} could not be ingested.")]
    private static partial void LogImagesFailed(
        ILogger logger,
        string version,
        int count,
        Exception exception);

    // Images of one version, and the memo their verdicts go to.
    private sealed record Batch(
        PatchVersion Version,
        List<DdragonImage> Images,
        VersionVerdicts Memo);
}
