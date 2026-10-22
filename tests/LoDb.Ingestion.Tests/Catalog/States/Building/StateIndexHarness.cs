using System.Text.Json;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Infrastructure.Persistence.States;
using LoDb.Infrastructure.Storage.Datasets;
using LoDb.Ingestion.Catalog.States.Promotion;
using LoDb.Ingestion.Catalog.States.Updates;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Normalization.Serialization;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Ingestion.Tests.Pipeline;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Ingestion.Tests.Catalog.States.Building;

/// <summary>
/// An API instance over a migrated database, whose store holds the en_US datasets of
/// test-owned versions that Data Dragon lists as the test says: versions are promoted, the
/// index of states updated, its rows read back.
/// </summary>
internal sealed class StateIndexHarness : IAsyncDisposable
{
    private const string VersionList = "/api/versions.json";
    private const string JsonType = "application/json";

    private static readonly TimeSpan BetweenPromotions = TimeSpan.FromHours(1);

    private readonly IngestionHarness harness;
    private readonly byte[] versionList;

    private StateIndexHarness(IngestionHarness harness, byte[] versionList)
    {
        this.harness = harness;
        this.versionList = versionList;
        Instance = StartInstance();
    }

    public IngestionInstance Instance { get; }

    public TestClock Clock => harness.Clock;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <param name="postgres">The server of the assembly.</param>
    /// <param name="listed">The versions Data Dragon lists, oldest first.</param>
    public static Task<StateIndexHarness> CreateAsync(
        PostgresContainerFixture postgres,
        params string[] listed) =>
        CreateAsync(postgres, new Dictionary<string, string?>(), listed);

    /// <param name="postgres">The server of the assembly.</param>
    /// <param name="settings">Settings of every instance, over the harness's.</param>
    /// <param name="listed">The versions Data Dragon lists, oldest first.</param>
    public static async Task<StateIndexHarness> CreateAsync(
        PostgresContainerFixture postgres,
        IReadOnlyDictionary<string, string?> settings,
        IReadOnlyList<string> listed)
    {
        var harness = await IngestionHarness.CreateAsync(postgres, settings);
        var body = JsonSerializer.SerializeToUtf8Bytes(listed.AsEnumerable().Reverse());
        return new StateIndexHarness(harness, body);
    }

    /// <summary>Another instance over the same database and store: another process.</summary>
    public IngestionInstance StartInstance()
    {
        var instance = harness.StartInstance();
        instance.Upstream.Serve(
            static url => url.AbsolutePath == VersionList,
            versionList,
            JsonType);
        return instance;
    }

    /// <summary>Stores the four en_US datasets of <paramref name="version"/>.</summary>
    public async Task StoreAsync(string version, VersionDatasets datasets)
    {
        var store = Instance.Get<IDatasetStore>();
        var scope = new DatasetScope
        {
            Version = PatchVersion.Parse(version),
            Language = DdragonLanguage.EnUs,
        };
        await WriteAsync(store, scope, DatasetTypes.Champions, datasets.Champions);
        await WriteAsync(store, scope, DatasetTypes.Items, datasets.Items);
        await WriteAsync(store, scope, DatasetTypes.Runes, datasets.Runes);
        await WriteAsync(store, scope, DatasetTypes.Summoners, datasets.Summoners);
    }

    /// <summary>Makes <paramref name="version"/> the latest one, an hour after the last.</summary>
    public async Task PromoteAsync(string version)
    {
        Clock.Advance(BetweenPromotions);
        var now = Clock.GetUtcNow();
        await using var db = harness.Database.CreateContext();
        db.DdragonVersions.Add(new DdragonVersion
        {
            Version = version,
            Status = DdragonVersionStatus.Ready,
            DiscoveredAt = now,
            UpdatedAt = now,
            ReadyAt = now,
            PromotedAt = now,
        });
        await db.SaveChangesAsync(Token);
    }

    /// <summary>Promotes <paramref name="version"/>, then tells the index's listener.</summary>
    public async Task PromoteAndListenAsync(string version)
    {
        await PromoteAsync(version);
        var listener = Instance.Get<IEnumerable<IPromotionListener>>()
            .OfType<StateIndexPromotionListener>()
            .Single();
        await listener.OnPromotedAsync(PatchVersion.Parse(version), Token);
    }

    public Task<StateIndexReport> UpdateAsync(StateIndexRequest request) =>
        Instance.Get<IStateIndexUpdater>().UpdateAsync(request, Token);

    public Task<StateIndexReport> BuildAsync() =>
        UpdateAsync(new StateIndexRequest { Trigger = StateIndexTrigger.Build });

    public Task<StateIndexReport> RebuildAsync() =>
        UpdateAsync(new StateIndexRequest
        {
            Trigger = StateIndexTrigger.Build,
            ShouldRebuild = true,
        });

    /// <summary>The states of the current generation, by resource, key and opener.</summary>
    public async Task<IReadOnlyList<CatalogEntityState>> StatesAsync()
    {
        await using var db = harness.Database.CreateContext();
        var states = await db.CatalogEntityStates.AsNoTracking()
            .Where(state => db.CatalogStateGenerations
                .Any(generation => generation.Id == state.GenerationId && generation.IsCurrent))
            .ToListAsync(Token);
        return
        [
            .. states.OrderBy(static state => state.Resource)
                .ThenBy(static state => state.EntityKey, StringComparer.Ordinal)
                .ThenBy(static state => PatchVersion.Parse(state.OpenerVersion)),
        ];
    }

    /// <summary>The covered versions of the current generation, oldest first.</summary>
    public async Task<IReadOnlyList<string>> CoverageAsync()
    {
        await using var db = harness.Database.CreateContext();
        return await db.CatalogStateVersions.AsNoTracking()
            .Where(version => db.CatalogStateGenerations
                .Any(generation => generation.Id == version.GenerationId && generation.IsCurrent))
            .OrderBy(static version => version.Ordinal)
            .Select(static version => version.Version)
            .ToListAsync(Token);
    }

    /// <summary>Every generation, current or not.</summary>
    public async Task<IReadOnlyList<CatalogStateGeneration>> GenerationsAsync()
    {
        await using var db = harness.Database.CreateContext();
        return await db.CatalogStateGenerations.AsNoTracking()
            .OrderBy(static generation => generation.Id)
            .ToListAsync(Token);
    }

    public ValueTask DisposeAsync() => harness.DisposeAsync();

    private static async Task WriteAsync<TEntry>(
        IDatasetStore store,
        DatasetScope scope,
        DatasetType<TEntry> type,
        DatasetDocument<TEntry> document)
    {
        var stored = document with { Version = scope.Version.Value };
        await store.WriteAsync(scope.Key(type), DatasetSerializer.Serialize(type, stored), Token);
    }
}
