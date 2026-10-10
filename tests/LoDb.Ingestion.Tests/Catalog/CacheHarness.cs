using System.Diagnostics.Metrics;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Pipeline.Datasets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// A <see cref="CatalogCache"/> with its gate and metrics, over catalogs of empty datasets:
/// the cache never looks inside a catalog.
/// </summary>
internal sealed class CacheHarness : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly ColdLoadGate gate;

    public CacheHarness(CatalogOptions options)
    {
        provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        Meters = provider.GetRequiredService<IMeterFactory>();
        var metrics = new CatalogMetrics(Meters);
        gate = new ColdLoadGate(Options.Create(options), TimeProvider.System, metrics);
        Cache = new CatalogCache(Options.Create(options), gate, metrics, TimeProvider.System);
    }

    public static PatchVersion Version { get; } = PatchVersion.Parse("16.19.1");

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public IMeterFactory Meters { get; }

    public CatalogCache Cache { get; }

    /// <summary>A gate that never stands in the way: the cache's own behavior is tested.</summary>
    public static CatalogOptions Wide(int maxEntries) => new()
    {
        MaxEntries = maxEntries,
        ColdLoadConcurrency = 64,
        ColdLoadsPerSecond = 100_000,
        PublicColdLoadsPerSecond = 50_000,
        ColdLoadBurst = 100_000,
    };

    public static CatalogKey Key(string language) => new(Version, DdragonLanguage.Parse(language));

    public static CatalogKey Key(int minor) =>
        new(PatchVersion.Parse($"9.{minor}.1"), DdragonLanguage.EnUs);

    public static ColdLoad Build(CatalogKey key, ColdCaller caller = ColdCaller.Internal) =>
        new(caller, _ => Task.FromResult<CatalogSnapshot?>(Snapshot(key)));

    /// <summary>A load that waits for the release of its hooks, told when it starts.</summary>
    public static ColdLoad Blocked(
        CatalogKey key,
        ColdCaller caller,
        (Task Release, Action Started) hooks) =>
        new(caller, async _ =>
        {
            hooks.Started();
            await hooks.Release;
            return Snapshot(key);
        });

    public static CatalogSnapshot Snapshot(CatalogKey key) =>
        new(
            key.Version,
            key.Language,
            new VersionDatasets
            {
                Champions = Empty<ChampionDetail>(key),
                Items = Empty<Item>(key),
                Runes = Empty<RuneTree>(key),
                Summoners = Empty<SummonerSpell>(key),
            },
            null);

    public void Dispose()
    {
        Cache.Dispose();
        gate.Dispose();
        provider.Dispose();
    }

    private static DatasetDocument<TEntry> Empty<TEntry>(CatalogKey key) => new()
    {
        Version = key.Version.Value,
        Language = key.Language.Code,
        Entries = [],
    };
}
