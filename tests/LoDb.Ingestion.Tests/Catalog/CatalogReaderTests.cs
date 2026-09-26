using System.Net;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Queue;
using LoDb.Ingestion.Tests.Pipeline;
using LoDb.Ingestion.Tests.Queue;
using LoDb.Testing;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The catalogs over the store and the recorded Data Dragon: a cold one ingested, queued or
/// left pending as the caller asks, held once loaded, never held after a failure.
/// </summary>
public sealed class CatalogReaderTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Latest = DdragonFixtures.Latest;
    private static readonly DdragonLanguage English = CatalogFixtures.English;
    private static readonly DdragonLanguage French = CatalogFixtures.French;

    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task ColdCatalogIsIngestedThenHeld()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var reader = instance.Get<ICatalogReader>();

        var cold = await reader.GetAsync(Latest, English, ColdDemand.Synchronous, Token);
        var fetched = instance.RequestedPaths(IsDataset).Count;
        Directory.Delete(Path.Combine(harness.StorageRoot, "data"), recursive: true);
        var held = await reader.GetAsync(Latest, English, ColdDemand.Synchronous, Token);

        Assert.True(cold.IsReady);
        Assert.Equal(5, cold.Catalog.Champions.Entries.Count);
        Assert.Same(cold.Catalog, held.Catalog);
        Assert.Equal(fetched, instance.RequestedPaths(IsDataset).Count);
    }

    [Fact]
    public async Task OtherLanguageIsReadOverEnglish()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();

        var load = await instance.Get<ICatalogReader>()
            .GetAsync(Latest, French, ColdDemand.Synchronous, Token);

        Assert.True(load.IsReady);
        Assert.Equal(French, load.Catalog.Language);
        Assert.Equal(4, harness.FilesUnder($"data/{Latest.Value}/en_US").Count);
        Assert.Equal(4, harness.FilesUnder($"data/{Latest.Value}/fr_FR").Count);
        var trinity = load.Catalog.Items.Find("3078")!;
        Assert.Equal("items/3078-trinity-force", load.Catalog.PathOf(trinity).Value);
        Assert.Equal(
            [new CatalogKey(Latest, French), new CatalogKey(Latest, English)],
            instance.Get<CatalogCache>().Keys);
        Assert.Contains(French, await instance.Get<ICatalogReader>().GetLanguagesAsync(Token));
    }

    [Fact]
    public async Task ConcurrentColdReadsShareOneCatalog()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var reader = instance.Get<ICatalogReader>();

        var loads = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            reader.GetAsync(Latest, French, ColdDemand.Synchronous, Token)));

        Assert.All(loads, load => Assert.Same(loads[0].Catalog, load.Catalog));
        Assert.NotNull(loads[0].Catalog);
        var fetched = instance.RequestedPaths(IsDataset);
        Assert.Equal(fetched.Count, fetched.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task QueuedReadIsPendingUntilTheWorkerIngests()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var reader = instance.Get<ICatalogReader>();
        var backlog = instance.Get<IOnDemandBacklog>();

        var pending = await reader.GetAsync(Latest, French, ColdDemand.Queued, Token);
        var queued = backlog.Count;
        var fetched = instance.RequestedPaths(IsDataset).Count;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var consuming = backlog.ConsumeAsync(stop.Token);
        await Eventually.UntilAsync(
            async () => (await reader.GetAsync(Latest, French, ColdDemand.StoredOnly, Token))
                .IsReady,
            Token);
        await stop.CancelAsync();

        Assert.Equal((CatalogLoadStatus.Pending, false), (pending.Status, pending.Refused));
        Assert.Null(pending.Catalog);
        Assert.Equal((2, 0), (queued, fetched));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consuming);
    }

    [Fact]
    public async Task StoredOnlyReadNeitherFetchesNorQueues()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();

        var load = await instance.Get<ICatalogReader>()
            .GetAsync(Latest, French, ColdDemand.StoredOnly, Token);

        Assert.Equal(CatalogLoadStatus.Pending, load.Status);
        Assert.Empty(instance.Replay.Requests);
        Assert.Equal(0, instance.Get<IOnDemandBacklog>().Count);
    }

    [Theory]
    [InlineData("99.1.1", "en_US", ColdPolicy.Synchronous)]
    [InlineData("99.1.1", "fr_FR", ColdPolicy.Queued)]
    [InlineData("16.19.1", "de_DE", ColdPolicy.Synchronous)]
    [InlineData("16.19.1", "de_DE", ColdPolicy.Queued)]
    public async Task UnlistedCatalogIsUnknown(string version, string language, ColdPolicy policy)
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();

        var load = await instance.Get<ICatalogReader>().GetAsync(
            PatchVersion.Parse(version),
            DdragonLanguage.Parse(language),
            new ColdDemand(policy),
            Token);

        Assert.Equal(CatalogLoadStatus.Unknown, load.Status);
        Assert.Empty(instance.RequestedPaths(IsDataset));
        Assert.Empty(harness.FilesUnder("data"));
        Assert.Equal(0, instance.Get<IOnDemandBacklog>().Count);
    }

    [Fact]
    public async Task CrawlerNeverWaitsForAnIngestion()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var demand = ColdDemand.Synchronous.For(OnDemandOrigin.Crawler);

        var load = await instance.Get<ICatalogReader>().GetAsync(Latest, English, demand, Token);

        Assert.Equal(ColdPolicy.Queued, demand.Effective);
        Assert.Equal(CatalogLoadStatus.Pending, load.Status);
        Assert.Equal(1, instance.Get<IOnDemandBacklog>().Count);
        Assert.Empty(instance.RequestedPaths(IsDataset));
    }

    [Fact]
    public async Task RefusedQueueIsReported()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            new Dictionary<string, string?> { ["LoDb:Ingestion:QueueCapacity"] = "1" });
        var instance = harness.StartInstance();

        var load = await instance.Get<ICatalogReader>()
            .GetAsync(Latest, French, ColdDemand.Queued, Token);

        Assert.Equal((CatalogLoadStatus.Pending, true), (load.Status, load.Refused));
        Assert.Equal(1, instance.Get<IOnDemandBacklog>().Count);
    }

    [Fact]
    public async Task TransientFailureIsThrownAndNeverHeld()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var reader = instance.Get<ICatalogReader>();
        // The first request and its three retries.
        instance.Upstream.FailNext(
            static url => url.AbsolutePath.EndsWith("/en_US/item.json", StringComparison.Ordinal),
            count: 4,
            HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAnyAsync<EgressException>(
            () => reader.GetAsync(Latest, English, ColdDemand.Synchronous, Token));
        var heldAfterFailure = instance.Get<CatalogCache>().Keys;
        var retried = await reader.GetAsync(Latest, English, ColdDemand.Synchronous, Token);

        Assert.Empty(heldAfterFailure);
        Assert.True(retried.IsReady);
    }

    [Fact]
    public async Task LatestVersionIsHeldForItsLifetime()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var reader = instance.Get<ICatalogReader>();

        var before = await reader.GetLatestAsync(Token);
        await VersionIngestionTests.IngestAsync(instance, Latest);
        var held = await reader.GetLatestAsync(Token);
        var fresh = await harness.StartInstance().Get<ICatalogReader>().GetVersionsAsync(Token);

        Assert.Null(before);
        Assert.Null(held);
        Assert.Equal(Latest, fresh.Latest);
        Assert.Equal([Latest], fresh.Ready);
        Assert.Equal(Latest, fresh.Listed[0]);
    }

    [Fact]
    public async Task LatestVersionIsReadAgainOnceExpired()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            new Dictionary<string, string?> { ["LoDb:Catalog:VersionsLifetime"] = "00:00:00.1" });
        var instance = harness.StartInstance();
        var reader = instance.Get<ICatalogReader>();

        var before = await reader.GetLatestAsync(Token);
        await VersionIngestionTests.IngestAsync(instance, Latest);
        // The lifetime runs on the wall clock of the memory cache; the test clock follows.
        harness.Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Null(before);
        await Eventually.UntilAsync(
            async () => await reader.GetLatestAsync(Token) == Latest,
            Token);
    }

    internal static bool IsDataset(Uri url) =>
        url.AbsolutePath.Contains("/data/", StringComparison.Ordinal);
}
