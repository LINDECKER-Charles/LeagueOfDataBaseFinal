using System.Collections.Concurrent;
using System.Threading.Channels;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Pipeline;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Pipeline.Versions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Queue;

/// <summary>
/// The long tail: synchronous ingestions merged by key, and a bounded queue without
/// duplicates for the background worker.
/// </summary>
/// <remarks>
/// A unit of work is the four datasets of a (version, language) or one image of a version.
/// A queued unit stays counted until the worker is done with it, so the same list asked for
/// again while cold queues nothing more.
/// </remarks>
internal sealed class OnDemandIngestion : IOnDemandIngestion, IOnDemandBacklog, IDisposable
{
    public const string QueueName = "on_demand";
    public const string FullReason = "full";
    public const string CrawlerBudgetReason = "crawler_budget";

    private readonly Channel<QueuedWork> channel;
    private readonly ConcurrentDictionary<string, byte> queued = new(StringComparer.Ordinal);
    private readonly SingleFlight flights = new();
    private readonly SemaphoreSlim slots;
    private readonly Services services;

    public OnDemandIngestion(
        KnownReleases releases,
        StoredDatasets stored,
        DatasetIngestion datasets,
        ImageManifest manifest,
        ImageIngestion images,
        CrawlerBudget budget,
        IngestionMetrics metrics,
        IOptions<IngestionOptions> options,
        ILogger<OnDemandIngestion> logger)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        services = new Services(
            releases, stored, datasets, manifest, images, budget, metrics, logger);
        slots = new SemaphoreSlim(options.Value.SyncConcurrency);
        channel = Channel.CreateBounded<QueuedWork>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                // Wait, not a drop mode: TryWrite then fails when the channel is full.
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
            });
        metrics.ObserveQueue(QueueName, () => channel.Reader.Count);
    }

    public int Count => channel.Reader.Count;

    public async Task<bool> EnsureDatasetsAsync(
        PatchVersion version,
        DdragonLanguage language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(language);
        var scope = new DatasetScope { Version = version, Language = language };
        if (await services.Stored.ExistAsync(scope, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (!await services.Releases.IsListedAsync(version, language, cancellationToken)
            .ConfigureAwait(false))
        {
            return false;
        }

        await flights.RunAsync(
                [DatasetUnit(version, language)],
                (_, token) => WithSlotAsync(
                    slotToken => services.Datasets.IngestAsync(scope, slotToken),
                    token),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public async Task EnsureImagesAsync(
        PatchVersion version,
        IReadOnlyCollection<DdragonImage> images,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(images);
        var unrecorded = await services.Manifest
            .UnrecordedAsync(version, images, cancellationToken)
            .ConfigureAwait(false);
        if (unrecorded.Count == 0
            || !await services.Releases.IsKnownAsync(version, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var byUnit = unrecorded
            .DistinctBy(image => ImageUnit(version, image))
            .ToDictionary(image => ImageUnit(version, image), StringComparer.Ordinal);
        await flights.RunAsync(
                byUnit.Keys,
                (units, token) => WithSlotAsync(
                    slotToken => services.Images.IngestAsync(
                        new ImageBatch(version, [.. units.Select(unit => byUnit[unit])], false),
                        slotToken),
                    token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public bool TryEnqueue(OnDemandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var units = UnitsOf(request);
        if (units.All(queued.ContainsKey))
        {
            return true;
        }

        if (request.Origin == OnDemandOrigin.Crawler && !services.Budget.TryTake())
        {
            services.Metrics.RecordRefusal(CrawlerBudgetReason);
            return false;
        }

        var claimed = units.Where(unit => queued.TryAdd(unit, 0)).ToList();
        if (claimed.Count == 0 || channel.Writer.TryWrite(Queued(request, claimed)))
        {
            return true;
        }

        Release(claimed);
        services.Metrics.RecordRefusal(FullReason);
        return false;
    }

    public async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        await foreach (var work in channel.Reader.ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            try
            {
                await ProcessAsync(work, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!IsStopping(exception, cancellationToken))
            {
                IngestionLog.OnDemandFailed(services.Logger, work.Version.Value, exception);
            }
            finally
            {
                Release(work.Units);
            }
        }
    }

    public void Dispose()
    {
        flights.Dispose();
        slots.Dispose();
    }

    private static string DatasetUnit(PatchVersion version, DdragonLanguage language) =>
        $"datasets|{version.Value}|{language.Code}";

    private static string ImageUnit(PatchVersion version, DdragonImage image) =>
        $"image|{version.Value}|{image.ManifestType}|{image.File}";

    private static List<string> UnitsOf(OnDemandRequest request)
    {
        List<string> units =
            [.. request.Images.Select(image => ImageUnit(request.Version, image)).Distinct()];
        if (request.Language is { } language)
        {
            units.Add(DatasetUnit(request.Version, language));
        }

        return units;
    }

    // Only the claimed units: the others are already waiting in another work.
    private static QueuedWork Queued(OnDemandRequest request, List<string> claimed)
    {
        var language = request.Language is { } asked
            && claimed.Contains(DatasetUnit(request.Version, asked))
                ? asked
                : null;
        var claimedSet = claimed.ToHashSet(StringComparer.Ordinal);
        List<DdragonImage> images =
        [
            .. request.Images
                .Where(image => claimedSet.Remove(ImageUnit(request.Version, image))),
        ];
        return new QueuedWork(request.Version, language, images, claimed);
    }

    private static bool IsStopping(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private async Task ProcessAsync(QueuedWork work, CancellationToken cancellationToken)
    {
        if (work.Language is { } language)
        {
            await EnsureDatasetsAsync(work.Version, language, cancellationToken)
                .ConfigureAwait(false);
        }

        if (work.Images.Count > 0)
        {
            await EnsureImagesAsync(work.Version, work.Images, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task WithSlotAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            slots.Release();
        }
    }

    private void Release(IEnumerable<string> units)
    {
        foreach (var unit in units)
        {
            queued.TryRemove(unit, out _);
        }
    }

    private sealed record QueuedWork(
        PatchVersion Version,
        DdragonLanguage? Language,
        IReadOnlyCollection<DdragonImage> Images,
        IReadOnlyList<string> Units);

    private sealed record Services(
        KnownReleases Releases,
        StoredDatasets Stored,
        DatasetIngestion Datasets,
        ImageManifest Manifest,
        ImageIngestion Images,
        CrawlerBudget Budget,
        IngestionMetrics Metrics,
        ILogger Logger);
}
