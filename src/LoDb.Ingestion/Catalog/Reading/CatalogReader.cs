using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog.Reading;

/// <inheritdoc cref="ICatalogReader"/>
internal sealed class CatalogReader(
    KnownReleases releases,
    VersionStates states,
    StoredDatasets stored,
    IOnDemandIngestion onDemand,
    CatalogCache cache,
    HybridCache hybridCache,
    IOptions<CatalogOptions> options) : ICatalogReader
{
    private const string ReadyKey = "lodb:catalog:ready-versions";

    private readonly HybridCacheEntryOptions readyEntry = new()
    {
        Expiration = options.Value.VersionsLifetime,
        LocalCacheExpiration = options.Value.VersionsLifetime,
    };

    public async Task<CatalogVersions> GetVersionsAsync(CancellationToken cancellationToken)
    {
        var listed = await releases.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var ready = await ReadyAsync(cancellationToken).ConfigureAwait(false);
        return new CatalogVersions
        {
            Latest = LatestOf(ready),
            Listed = listed,
            Ready = [.. ready.Codes.Select(PatchVersion.Parse)],
        };
    }

    public async Task<PatchVersion?> GetLatestAsync(CancellationToken cancellationToken) =>
        LatestOf(await ReadyAsync(cancellationToken).ConfigureAwait(false));

    public Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(
        CancellationToken cancellationToken) =>
        releases.GetLanguagesAsync(cancellationToken);

    public async Task<CatalogLoad> GetAsync(
        PatchVersion version,
        DdragonLanguage language,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(demand);
        if (cache.TryGet(new CatalogKey(version, language), out var held))
        {
            return CatalogLoad.Ready(held);
        }

        var scope = new DatasetScope { Version = version, Language = language };
        if (await EnsureStoredAsync(ScopesOf(scope), demand, cancellationToken)
            .ConfigureAwait(false) is { } unavailable)
        {
            return unavailable;
        }

        // Null when a dataset left the store since it was checked.
        return await BuildAsync(scope, cancellationToken).ConfigureAwait(false) is { } catalog
            ? CatalogLoad.Ready(catalog)
            : CatalogLoad.Pending(refused: false);
    }

    private static PatchVersion? LatestOf(ReadyVersions ready) =>
        ready.Latest is { } latest ? PatchVersion.Parse(latest) : null;

    // The requested scope first: an unlisted language stores nothing, not even en_US.
    private static DatasetScope[] ScopesOf(DatasetScope scope) =>
        scope.Language == DdragonLanguage.EnUs
            ? [scope]
            : [scope, scope with { Language = DdragonLanguage.EnUs }];

    // The en_US catalog first: the other languages read their slugs and tokens from it.
    private async Task<CatalogSnapshot?> BuildAsync(
        DatasetScope scope,
        CancellationToken cancellationToken)
    {
        if (scope.Language == DdragonLanguage.EnUs)
        {
            return await LoadAsync(scope, null, cancellationToken).ConfigureAwait(false);
        }

        var english = scope with { Language = DdragonLanguage.EnUs };
        return await LoadAsync(english, null, cancellationToken).ConfigureAwait(false)
            is { } englishCatalog
                ? await LoadAsync(scope, englishCatalog, cancellationToken).ConfigureAwait(false)
                : null;
    }

    // Null once every dataset is stored; otherwise what the read answers instead.
    private async Task<CatalogLoad?> EnsureStoredAsync(
        DatasetScope[] scopes,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        List<DatasetScope> missing = [];
        foreach (var scope in scopes)
        {
            if (!await stored.ExistAsync(scope, cancellationToken).ConfigureAwait(false))
            {
                missing.Add(scope);
            }
        }

        if (missing.Count == 0)
        {
            return null;
        }

        return demand.Effective switch
        {
            ColdPolicy.Synchronous => await IngestAsync(missing, cancellationToken)
                .ConfigureAwait(false),
            ColdPolicy.Queued => await QueueAsync(scopes[0], missing, demand, cancellationToken)
                .ConfigureAwait(false),
            _ => CatalogLoad.Pending(refused: false),
        };
    }

    private async Task<CatalogLoad?> IngestAsync(
        List<DatasetScope> missing,
        CancellationToken cancellationToken)
    {
        foreach (var scope in missing)
        {
            var listed = await onDemand
                .EnsureDatasetsAsync(scope.Version, scope.Language, cancellationToken)
                .ConfigureAwait(false);
            if (!listed)
            {
                return CatalogLoad.Unknown;
            }
        }

        return null;
    }

    private async Task<CatalogLoad> QueueAsync(
        DatasetScope requested,
        List<DatasetScope> missing,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        if (!await releases.IsListedAsync(requested.Version, requested.Language, cancellationToken)
            .ConfigureAwait(false))
        {
            return CatalogLoad.Unknown;
        }

        var accepted = true;
        foreach (var scope in missing)
        {
            accepted &= onDemand.TryEnqueue(new OnDemandRequest
            {
                Version = scope.Version,
                Language = scope.Language,
                Origin = demand.Origin,
            });
        }

        return CatalogLoad.Pending(refused: !accepted);
    }

    private Task<CatalogSnapshot?> LoadAsync(
        DatasetScope scope,
        CatalogSnapshot? english,
        CancellationToken cancellationToken) =>
        cache.GetOrLoadAsync(
            new CatalogKey(scope.Version, scope.Language),
            async token => await stored.LoadAsync(scope, token).ConfigureAwait(false)
                is { } datasets
                    ? new CatalogSnapshot(scope.Version, scope.Language, datasets, english)
                    : null,
            cancellationToken);

    private ValueTask<ReadyVersions> ReadyAsync(CancellationToken cancellationToken) =>
        hybridCache.GetOrCreateAsync(
            ReadyKey,
            states,
            static async (source, token) =>
            {
                var (ready, latest) = await source.ReadyAsync(token).ConfigureAwait(false);
                return new ReadyVersions([.. ready.Select(static v => v.Value)], latest?.Value);
            },
            readyEntry,
            cancellationToken: cancellationToken);
}
