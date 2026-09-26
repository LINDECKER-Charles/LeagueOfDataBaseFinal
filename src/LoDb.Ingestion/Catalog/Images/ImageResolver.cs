using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Infrastructure.Storage.Blobs;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Catalog.Images;

/// <inheritdoc cref="IImageResolver"/>
internal sealed partial class ImageResolver(
    ImageManifest manifest,
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
        var rows = await manifest.FindAsync(version, wanted, cancellationToken)
            .ConfigureAwait(false);
        List<DdragonImage> unknown = [.. wanted.Where(image => !rows.ContainsKey(KeyOf(image)))];
        var refused = unknown.Count > 0
            && await SettleAsync(version, unknown, rows, demand, cancellationToken)
                .ConfigureAwait(false);
        return new ImageResolution(
            wanted.ToDictionary(KeyOf, image => Resolve(rows, image)),
            refused);
    }

    private static (string Type, string File) KeyOf(DdragonImage image) =>
        (image.ManifestType, image.File);

    private static ResolvedImage Resolve(
        Dictionary<(string Type, string Key), DdragonAsset> rows,
        DdragonImage image) =>
        rows.TryGetValue(KeyOf(image), out var row) ? Verdict(row) : ResolvedImage.Pending;

    private static ResolvedImage Verdict(DdragonAsset row) =>
        row is { Status: DdragonAssetStatus.Present, Sha256: { } sha, Extension: { } extension }
            ? ResolvedImage.Present(new BlobKey(sha, extension))
            : ResolvedImage.Absent;

    // Records the verdicts it waited for into the rows; true when the queue refused the work.
    private async Task<bool> SettleAsync(
        PatchVersion version,
        List<DdragonImage> unknown,
        Dictionary<(string Type, string Key), DdragonAsset> rows,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        switch (demand.Effective)
        {
            case ColdPolicy.Synchronous:
                await WaitAsync(version, unknown, rows, cancellationToken).ConfigureAwait(false);
                return false;
            case ColdPolicy.Queued:
                return !onDemand.TryEnqueue(new OnDemandRequest
                {
                    Version = version,
                    Images = unknown,
                    Origin = demand.Origin,
                });
            default:
                return false;
        }
    }

    // An upstream failure leaves the images pending: the page still renders, with placeholders.
    private async Task WaitAsync(
        PatchVersion version,
        List<DdragonImage> unknown,
        Dictionary<(string Type, string Key), DdragonAsset> rows,
        CancellationToken cancellationToken)
    {
        try
        {
            await onDemand.EnsureImagesAsync(version, unknown, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (EgressException exception)
        {
            LogImagesFailed(logger, version.Value, unknown.Count, exception);
            return;
        }

        var settled = await manifest.FindAsync(version, unknown, cancellationToken)
            .ConfigureAwait(false);
        foreach (var (key, row) in settled)
        {
            rows[key] = row;
        }
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
}
