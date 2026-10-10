using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using static LoDb.Ingestion.Tests.Catalog.CacheHarness;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The catalogs in memory: the pinned ones never evicted, the others the least recently read
/// evicted first, one load per cold key whatever the readers, and nothing kept from a load
/// that failed or found nothing.
/// </summary>
public sealed class CatalogCacheTests
{
    private const int SweptKeys = 1000;
    private const int RecentPlaces = 16;

    [Fact]
    public async Task LeastRecentlyReadIsEvictedFirst()
    {
        using var harness = new CacheHarness(Wide(maxEntries: 2));
        var cache = harness.Cache;
        var english = Key("en_US");
        var french = Key("fr_FR");
        var korean = Key("ko_KR");

        await cache.GetOrLoadAsync(english, Build(english), Token);
        await cache.GetOrLoadAsync(french, Build(french), Token);
        Assert.True(cache.TryGet(english, out _));
        await cache.GetOrLoadAsync(korean, Build(korean), Token);

        Assert.Equal([korean, english], cache.Keys);
        Assert.False(cache.TryGet(french, out _));
    }

    [Fact]
    public async Task PinnedCatalogsSurviveASweepOfAThousandKeys()
    {
        using var harness = new CacheHarness(Wide(RecentPlaces));
        using var evictions = new MetricCollector<long>(
            harness.Meters, CatalogMetrics.MeterName, "lodb.catalog.cache.evictions");
        var cache = harness.Cache;
        CatalogKey[] pinned = [Key("en_US"), Key("fr_FR"), Key("ko_KR"), Key("zh_CN")];
        cache.Pin(pinned);
        var held = new Dictionary<CatalogKey, CatalogSnapshot>();
        foreach (var key in pinned)
        {
            held[key] = (await cache.GetOrLoadAsync(key, Build(key), Token)).Catalog!;
        }

        for (var minor = 0; minor < SweptKeys; minor++)
        {
            await cache.GetOrLoadAsync(Key(minor), Build(Key(minor)), Token);
        }

        Assert.All(pinned, key =>
        {
            Assert.True(cache.TryGet(key, out var catalog));
            Assert.Same(held[key], catalog);
        });
        Assert.True(cache.PinnedKeys.ToHashSet().SetEquals(pinned));
        Assert.Equal(RecentPlaces, cache.Keys.Count);
        Assert.Equal(
            SweptKeys - RecentPlaces,
            evictions.GetMeasurementSnapshot().EvaluateAsCounter());
    }

    [Fact]
    public async Task PinningMovesHeldCatalogsBetweenTiers()
    {
        using var harness = new CacheHarness(Wide(maxEntries: 2));
        var cache = harness.Cache;
        var english = Key("en_US");
        var french = Key("fr_FR");
        await cache.GetOrLoadAsync(english, Build(english), Token);

        cache.Pin([english, french]);
        Assert.Equal([english], cache.PinnedKeys);
        Assert.Empty(cache.Keys);
        await cache.GetOrLoadAsync(french, Build(french), Token);
        Assert.True(cache.PinnedKeys.ToHashSet().SetEquals([english, french]));

        cache.Pin([french]);
        Assert.Equal([french], cache.PinnedKeys);
        Assert.Equal([english], cache.Keys);
    }

    [Fact]
    public async Task HeldCatalogIsNotLoadedAgain()
    {
        using var harness = new CacheHarness(Wide(maxEntries: 2));
        var key = Key("en_US");
        var loads = 0;

        var first = await harness.Cache.GetOrLoadAsync(key, Count(key, () => loads++), Token);
        var second = await harness.Cache.GetOrLoadAsync(key, Count(key, () => loads++), Token);

        Assert.Equal(1, loads);
        Assert.Same(first.Catalog, second.Catalog);
    }

    [Fact]
    public async Task ConcurrentReadersShareOneLoad()
    {
        using var harness = new CacheHarness(Wide(maxEntries: 2));
        using var lookups = new MetricCollector<long>(
            harness.Meters, CatalogMetrics.MeterName, "lodb.catalog.cache.lookups");
        var key = Key("fr_FR");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loads = 0;
        using var giveUp = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var load = Blocked(
            key,
            ColdCaller.Public,
            (release.Task, () => Interlocked.Increment(ref loads)));

        var impatient = harness.Cache.GetOrLoadAsync(key, load, giveUp.Token);
        var readers = Enumerable.Range(0, 4)
            .Select(_ => harness.Cache.GetOrLoadAsync(key, load, Token))
            .ToList();
        await giveUp.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => impatient);
        release.SetResult();
        var reads = await Task.WhenAll(readers);

        Assert.Equal(1, loads);
        Assert.All(reads, read => Assert.Same(reads[0].Catalog, read.Catalog));
        Assert.Equal([key], harness.Cache.Keys);
        Assert.Equal(1, Outcomes(lookups, CatalogMetrics.MissOutcome));
        Assert.Equal(4, Outcomes(lookups, CatalogMetrics.JoinedOutcome));
    }

    [Fact]
    public async Task FailedLoadIsNotKept()
    {
        using var harness = new CacheHarness(Wide(maxEntries: 2));
        var cache = harness.Cache;
        var key = Key("en_US");

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrLoadAsync(
            key,
            new ColdLoad(ColdCaller.Internal, static _ => throw new InvalidOperationException()),
            Token));
        Assert.Empty(cache.Keys);
        var read = await cache.GetOrLoadAsync(key, Build(key), Token);

        Assert.True(read.IsHeld);
        Assert.Equal([key], cache.Keys);
    }

    [Fact]
    public async Task MissingDatasetsAreNotKept()
    {
        using var harness = new CacheHarness(Wide(maxEntries: 2));
        var cache = harness.Cache;
        var key = Key("en_US");

        var missing = await cache.GetOrLoadAsync(
            key,
            new ColdLoad(ColdCaller.Internal, static _ => Task.FromResult<CatalogSnapshot?>(null)),
            Token);

        Assert.Same(CacheRead.Missing, missing);
        Assert.False(missing.IsRefused);
        Assert.Empty(cache.Keys);
        Assert.True((await cache.GetOrLoadAsync(key, Build(key), Token)).IsHeld);
    }

    [Fact]
    public async Task DisposedCacheRefusesReads()
    {
        var harness = new CacheHarness(Wide(maxEntries: 2));
        harness.Cache.Dispose();
        harness.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => harness.Cache.GetOrLoadAsync(Key("en_US"), Build(Key("en_US")), Token));
    }

    private static ColdLoad Count(CatalogKey key, Action loaded) =>
        new(ColdCaller.Internal, _ =>
        {
            loaded();
            return Task.FromResult<CatalogSnapshot?>(Snapshot(key));
        });

    private static long Outcomes(MetricCollector<long> lookups, string outcome) =>
        lookups.GetMeasurementSnapshot()
            .Where(measurement => Equals(measurement.Tags[CatalogMetrics.OutcomeTag], outcome))
            .Sum(static measurement => measurement.Value);
}
