using LoDb.Domain.Versions;
using LoDb.Infrastructure.Storage.Datasets;
using LoDb.Ingestion.Normalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog.Reading;

/// <inheritdoc cref="ICatalogPinning"/>
/// <remarks>
/// The loads go through the cold gate as the renderer's do, one after the other: at a
/// promotion the renderer wants the same catalogs, and joins them. The budget is checked
/// against an estimate, the stored JSON times what a built catalog weighs per stored byte.
/// </remarks>
internal sealed partial class CatalogPinning(
    ICatalogReader reader,
    CatalogCache cache,
    IDatasetStore store,
    IOptions<CatalogOptions> options,
    ILogger<CatalogPinning> logger) : ICatalogPinning, IDisposable
{
    // Retained memory of a built catalog per byte of its four stored datasets: 2.05 to 2.71 on
    // the real datasets of 16.20.1 (Latin, Cyrillic, Thai, Arabic and CJK languages, about
    // 7 MiB a catalog), rounded up.
    private const double RetainedBytesPerStoredByte = 2.75;
    private const long BytesPerMegabyte = 1024 * 1024;

    private static readonly ColdDemand PinDemand = ColdDemand.StoredOnly.By(ColdCaller.Internal);

    private readonly SemaphoreSlim serial = new(1, 1);
    private PinPlan? plan;

    public async Task PinLatestAsync(CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PinAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            serial.Release();
        }
    }

    public void Dispose() => serial.Dispose();

    private async Task PinAsync(CancellationToken cancellationToken)
    {
        if (await reader.GetLatestAsync(cancellationToken).ConfigureAwait(false)
            is not { } latest)
        {
            return;
        }

        if (plan is null || plan.Version != latest)
        {
            plan = await PlanAsync(latest, cancellationToken).ConfigureAwait(false);
            cache.Pin(plan.Keys);
        }

        if (!plan.IsHeld && await LoadAsync(plan, cancellationToken).ConfigureAwait(false))
        {
            plan = plan with { IsHeld = true };
            LogPinned(logger, latest.Value, plan.Keys.Count, plan.Megabytes, plan.LeftOut);
        }
    }

    // The languages in their order, as long as their estimate fits in the budget; one whose
    // datasets are not stored is left out.
    private async Task<PinPlan> PlanAsync(PatchVersion latest, CancellationToken cancellationToken)
    {
        var budget = options.Value.PinnedBudgetMegabytes * BytesPerMegabyte;
        var languages = PinnedLanguageList.Parse(options.Value.PinnedLanguages)
            ?? PinnedLanguageList.Defaults;
        List<CatalogKey> keys = [];
        long estimated = 0;
        foreach (var language in languages)
        {
            var scope = new DatasetScope { Version = latest, Language = language };
            if (await EstimateAsync(scope, cancellationToken).ConfigureAwait(false)
                is not { } size)
            {
                continue;
            }

            if (estimated + size > budget)
            {
                break;
            }

            estimated += size;
            keys.Add(new CatalogKey(latest, language));
        }

        var leftOut = languages.Count - keys.Count;
        return new PinPlan(latest, keys, estimated / BytesPerMegabyte, leftOut);
    }

    // What the built catalog should weigh; null while a dataset is not stored.
    private async Task<long?> EstimateAsync(DatasetScope scope, CancellationToken cancellationToken)
    {
        DatasetKey[] datasets =
        [
            scope.Key(DatasetTypes.Champions),
            scope.Key(DatasetTypes.Items),
            scope.Key(DatasetTypes.Runes),
            scope.Key(DatasetTypes.Summoners),
        ];
        long stored = 0;
        foreach (var dataset in datasets)
        {
            var json = await store.OpenReadAsync(dataset, cancellationToken).ConfigureAwait(false);
            if (json is null)
            {
                return null;
            }

            await using (json.ConfigureAwait(false))
            {
                stored += json.Length;
            }
        }

        return (long)Math.Ceiling(stored * RetainedBytesPerStoredByte);
    }

    // Whether every pinned catalog is held; a load left pending or refused waits for the next
    // call.
    private async Task<bool> LoadAsync(PinPlan pinned, CancellationToken cancellationToken)
    {
        var held = cache.PinnedKeys;
        var complete = true;
        foreach (var key in pinned.Keys.Where(key => !held.Contains(key)))
        {
            var load = await reader
                .GetAsync(key.Version, key.Language, PinDemand, cancellationToken)
                .ConfigureAwait(false);
            complete &= load.IsReady;
        }

        return complete;
    }

    [LoggerMessage(
        EventName = "catalog.pinning.completed",
        Level = LogLevel.Information,
        Message = "Pinned {Count} catalogs of version {Version}, about {Megabytes} MiB;"
            + " {LeftOut} languages left out of the budget.")]
    private static partial void LogPinned(
        ILogger logger,
        string version,
        int count,
        long megabytes,
        int leftOut);

    /// <summary>The catalogs pinned for a version, and whether they are all held.</summary>
    private sealed record PinPlan(
        PatchVersion Version,
        IReadOnlyList<CatalogKey> Keys,
        long Megabytes,
        int LeftOut)
    {
        public bool IsHeld { get; init; }
    }
}
