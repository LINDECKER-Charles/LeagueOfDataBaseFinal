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
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The catalogs in memory: the least recently read evicted first, one load per cold key
/// whatever the readers, and nothing kept from a load that failed or found nothing.
/// </summary>
public sealed class CatalogCacheTests
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.19.1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LeastRecentlyReadIsEvictedFirst()
    {
        using var cache = Create(maxEntries: 2);
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
    public async Task HeldCatalogIsNotLoadedAgain()
    {
        using var cache = Create(maxEntries: 2);
        var key = Key("en_US");
        var loads = 0;

        var first = await cache.GetOrLoadAsync(key, Count(key, () => loads++), Token);
        var second = await cache.GetOrLoadAsync(key, Count(key, () => loads++), Token);

        Assert.Equal(1, loads);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task ConcurrentReadersShareOneLoad()
    {
        using var cache = Create(maxEntries: 2);
        var key = Key("fr_FR");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loads = 0;
        using var giveUp = CancellationTokenSource.CreateLinkedTokenSource(Token);

        Task<CatalogSnapshot?> Read(CancellationToken token) =>
            cache.GetOrLoadAsync(
                key,
                async _ =>
                {
                    Interlocked.Increment(ref loads);
                    await gate.Task;
                    return Snapshot(key);
                },
                token);

        var impatient = Read(giveUp.Token);
        var readers = Enumerable.Range(0, 4).Select(_ => Read(Token)).ToList();
        await giveUp.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => impatient);
        gate.SetResult();
        var catalogs = await Task.WhenAll(readers);

        Assert.Equal(1, loads);
        Assert.All(catalogs, catalog => Assert.Same(catalogs[0], catalog));
        Assert.Equal([key], cache.Keys);
    }

    [Fact]
    public async Task FailedLoadIsNotKept()
    {
        using var cache = Create(maxEntries: 2);
        var key = Key("en_US");

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrLoadAsync(
            key,
            static _ => throw new InvalidOperationException("upstream"),
            Token));
        Assert.Empty(cache.Keys);
        var catalog = await cache.GetOrLoadAsync(key, Build(key), Token);

        Assert.NotNull(catalog);
        Assert.Equal([key], cache.Keys);
    }

    [Fact]
    public async Task MissingDatasetsAreNotKept()
    {
        using var cache = Create(maxEntries: 2);
        var key = Key("en_US");

        var missing = await cache.GetOrLoadAsync(
            key,
            static _ => Task.FromResult<CatalogSnapshot?>(null),
            Token);

        Assert.Null(missing);
        Assert.Empty(cache.Keys);
        Assert.NotNull(await cache.GetOrLoadAsync(key, Build(key), Token));
    }

    [Fact]
    public async Task DisposedCacheRefusesReads()
    {
        var cache = Create(maxEntries: 2);
        cache.Dispose();
        cache.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => cache.GetOrLoadAsync(Key("en_US"), Build(Key("en_US")), Token));
    }

    private static CatalogCache Create(int maxEntries) =>
        new(Options.Create(new CatalogOptions { MaxEntries = maxEntries }));

    private static CatalogKey Key(string language) =>
        new(Version, DdragonLanguage.Parse(language));

    private static Func<CancellationToken, Task<CatalogSnapshot?>> Build(CatalogKey key) =>
        _ => Task.FromResult<CatalogSnapshot?>(Snapshot(key));

    private static Func<CancellationToken, Task<CatalogSnapshot?>> Count(
        CatalogKey key,
        Action loaded) =>
        _ =>
        {
            loaded();
            return Task.FromResult<CatalogSnapshot?>(Snapshot(key));
        };

    // A catalog of empty datasets: the cache never looks inside.
    private static CatalogSnapshot Snapshot(CatalogKey key) =>
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

    private static DatasetDocument<TEntry> Empty<TEntry>(CatalogKey key) => new()
    {
        Version = key.Version.Value,
        Language = key.Language.Code,
        Entries = [],
    };
}
