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
/// <para>
/// A unit of work is the four datasets of a (version, language) or one image of a version.
/// A queued unit stays counted until the worker is done with it, so the same list asked for
/// again while cold queues nothing more.
/// </para>
/// <para>
/// A visitor's synchronous work takes the per-minute budgets of cold work and waits a
/// bounded time for its slot (<see cref="SyncSlots"/>); a unit already in flight is joined
/// for free. The worker's work was admitted when it was queued: no budget, no deadline.
/// </para>
/// </remarks>
internal sealed class OnDemandIngestion : IOnDemandIngestion, IOnDemandBacklog, IDisposable
{
    public const string QueueName = "on_demand";
    public const string FullReason = "full";
    public const string CrawlerBudgetReason = "crawler_budget";
    public const string DatasetBudgetReason = "dataset_budget";
    public const string ImageBudgetReason = "image_budget";

    private readonly Channel<QueuedWork> channel;
    private readonly ConcurrentDictionary<string, byte> queued = new(StringComparer.Ordinal);
    private readonly SingleFlight flights = new();
    private readonly Services services;
    private readonly OnDemandLane visitor;
    private readonly OnDemandLane worker;

    public OnDemandIngestion(
        KnownReleases releases,
        StoredDatasets stored,
        DatasetIngestion datasets,
        ImageManifest manifest,
        ImageIngestion images,
        OnDemandBudgets budgets,
        SyncSlots slots,
        IngestionMetrics metrics,
        IOptions<IngestionOptions> options,
        ILogger<OnDemandIngestion> logger)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        services = new Services(
            releases, stored, datasets, manifest, images, budgets, metrics, logger);
        visitor = OnDemandLane.Visitor(budgets, slots);
        worker = OnDemandLane.Worker(slots);
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

    public Task<bool> EnsureDatasetsAsync(
        PatchVersion version,
        DdragonLanguage language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(language);
        var scope = new DatasetScope { Version = version, Language = language };
        return EnsureDatasetsAsync(scope, visitor, cancellationToken);
    }

    public Task EnsureImagesAsync(
        PatchVersion version,
        IReadOnlyCollection<DdragonImage> images,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(images);
        return EnsureAsync(new ImageBatch(version, images, false), visitor, cancellationToken);
    }

    public Task EnsureImagesAsync(WatchedImages images, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);
        var batch = new ImageBatch(images.Version, images.Images, false)
        {
            Progress = images.Progress,
        };
        return EnsureAsync(batch, visitor, cancellationToken);
    }

    public bool TryEnqueue(OnDemandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var units = OnDemandUnits.Of(request);
        if (units.All(queued.ContainsKey))
        {
            return true;
        }

        if (request.Origin == OnDemandOrigin.Crawler && !services.Budgets.Crawler.TryTake())
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

    public void Dispose() => flights.Dispose();

    // Only the claimed units: the others are already waiting in another work.
    private static QueuedWork Queued(OnDemandRequest request, List<string> claimed)
    {
        var language = request.Language is { } asked
            && claimed.Contains(OnDemandUnits.Dataset(request.Version, asked))
                ? asked
                : null;
        var claimedSet = claimed.ToHashSet(StringComparer.Ordinal);
        List<DdragonImage> images =
        [
            .. request.Images.Where(image =>
                claimedSet.Remove(OnDemandUnits.Image(request.Version, image))),
        ];
        return new QueuedWork(request.Version, language, images, claimed);
    }

    private static bool IsStopping(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private async Task ProcessAsync(QueuedWork work, CancellationToken cancellationToken)
    {
        if (work.Language is { } language)
        {
            var scope = new DatasetScope { Version = work.Version, Language = language };
            await EnsureDatasetsAsync(scope, worker, cancellationToken).ConfigureAwait(false);
        }

        if (work.Images.Count > 0)
        {
            var batch = new ImageBatch(work.Version, work.Images, false);
            await EnsureAsync(batch, worker, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> EnsureDatasetsAsync(
        DatasetScope scope,
        OnDemandLane lane,
        CancellationToken cancellationToken)
    {
        if (await services.Stored.ExistAsync(scope, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (!await services.Releases
            .IsListedAsync(scope.Version, scope.Language, cancellationToken)
            .ConfigureAwait(false))
        {
            return false;
        }

        string[] unit = [OnDemandUnits.Dataset(scope.Version, scope.Language)];
        if (lane.AdmitDatasets(flights, unit).Count == 0)
        {
            throw Refuse(DatasetBudgetReason);
        }

        await flights.RunAsync(
                unit,
                (_, token) => lane.RunAsync(
                    slotToken => services.Datasets.IngestAsync(scope, slotToken),
                    token),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    // The flight covering a unit runs only the images it claimed, with the claimer's progress.
    // The images over budget stay without a verdict, and the caller is told once the others
    // have landed.
    private async Task EnsureAsync(
        ImageBatch requested,
        OnDemandLane lane,
        CancellationToken cancellationToken)
    {
        var version = requested.Version;
        var unrecorded = await services.Manifest
            .UnrecordedAsync(version, requested.Images, cancellationToken)
            .ConfigureAwait(false);
        if (unrecorded.Count == 0
            || !await services.Releases.IsKnownAsync(version, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var byUnit = unrecorded
            .DistinctBy(image => OnDemandUnits.Image(version, image))
            .ToDictionary(
                image => OnDemandUnits.Image(version, image),
                StringComparer.Ordinal);
        var admitted = lane.AdmitImages(flights, byUnit.Keys);
        await flights.RunAsync(
                admitted,
                (units, token) => lane.RunAsync(
                    slotToken => services.Images.IngestAsync(
                        requested with { Images = [.. units.Select(unit => byUnit[unit])] },
                        slotToken),
                    token),
                cancellationToken)
            .ConfigureAwait(false);
        if (admitted.Count < byUnit.Count)
        {
            throw Refuse(ImageBudgetReason);
        }
    }

    private OnDemandRefusedException Refuse(string reason)
    {
        services.Metrics.RecordRefusal(reason);
        return new OnDemandRefusedException(reason);
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
        OnDemandBudgets Budgets,
        IngestionMetrics Metrics,
        ILogger Logger);
}
