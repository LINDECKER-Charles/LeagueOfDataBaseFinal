using System.Collections.Concurrent;
using System.Net;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Queue;
using LoDb.Ingestion.Tests.Pipeline;
using LoDb.Testing;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Queue;

/// <summary>
/// The synchronous path of the long tail: what a page needs is ingested at once, merged
/// across concurrent requests, and never fetched for a version or a language Data Dragon
/// does not list.
/// </summary>
public sealed class OnDemandIngestionTests(PostgresContainerFixture postgres)
{
    internal static readonly DdragonLanguage French = DdragonLanguage.Parse("fr_FR");
    internal static readonly DdragonImage Ahri = new(DdragonImageKind.Champion, "Ahri.png");
    internal static readonly DdragonImage Garen = new(DdragonImageKind.Champion, "Garen.png");
    internal static readonly DdragonImage Teemo = new(DdragonImageKind.Champion, "Teemo.png");

    private static readonly PatchVersion Latest = DdragonFixtures.Latest;

    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task ConcurrentRequestsForADatasetFetchItOnce()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var ingestion = instance.Get<IOnDemandIngestion>();

        var answers = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => ingestion.EnsureDatasetsAsync(Latest, French, Token)));

        Assert.All(answers, Assert.True);
        var fetched = instance.RequestedPaths(IsDataset);
        Assert.NotEmpty(fetched);
        Assert.Equal(fetched.Count, fetched.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(4, harness.FilesUnder($"data/{Latest.Value}/fr_FR").Count);
        Assert.True(await ingestion.EnsureDatasetsAsync(Latest, French, Token));
        Assert.Equal(fetched.Count, instance.RequestedPaths(IsDataset).Count);
    }

    [Theory]
    [InlineData("16.19.1", "de_DE")]
    [InlineData("99.1.1", "en_US")]
    public async Task UnlistedVersionOrLanguageIsNotFetched(string version, string language)
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();

        var listed = await instance.Get<IOnDemandIngestion>().EnsureDatasetsAsync(
            PatchVersion.Parse(version),
            DdragonLanguage.Parse(language),
            Token);

        Assert.False(listed);
        Assert.Empty(instance.RequestedPaths(IsDataset));
        Assert.Empty(harness.FilesUnder("data"));
    }

    [Fact]
    public async Task EveryImageGetsItsVerdictButTheTransientOnes()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        instance.Replay.FailWith(static url => Is(url, Garen), HttpStatusCode.Forbidden);
        instance.Replay.FailWith(static url => Is(url, Teemo), HttpStatusCode.ServiceUnavailable);

        await instance.Get<IOnDemandIngestion>()
            .EnsureImagesAsync(Latest, [Ahri, Garen, Teemo, Ahri], Token);

        var rows = (await harness.AssetRowsAsync(Latest.Value))
            .ToDictionary(static row => row.Key, StringComparer.Ordinal);
        Assert.Equal(2, rows.Count);
        Assert.Equal(DdragonAssetStatus.Present, rows[Ahri.File].Status);
        Assert.Equal(DdragonAssetStatus.Absent, rows[Garen.File].Status);
        Assert.Single(instance.Replay.Requests, static url => Is(url, Ahri));
    }

    [Fact]
    public async Task SettledImagesAreNotFetchedAgain()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        await harness.StartInstance().Get<IOnDemandIngestion>()
            .EnsureImagesAsync(Latest, [Ahri, Garen], Token);
        var instance = harness.StartInstance();

        await instance.Get<IOnDemandIngestion>().EnsureImagesAsync(Latest, [Ahri, Garen], Token);

        Assert.Empty(instance.Replay.Requests);
    }

    [Fact]
    public async Task AWatcherHearsOfEachImageTheCallFetches()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        await harness.StartInstance().Get<IOnDemandIngestion>()
            .EnsureImagesAsync(Latest, [Garen], Token);
        var instance = harness.StartInstance();
        instance.Replay.FailWith(static url => Is(url, Teemo), HttpStatusCode.ServiceUnavailable);
        var watcher = new Watcher();

        await instance.Get<IOnDemandIngestion>().EnsureImagesAsync(
            new WatchedImages { Version = Latest, Images = [Ahri, Garen, Teemo], Progress = watcher },
            Token);

        // Garen was settled before: skipped, unheard. Teemo failed: fetched all the same.
        Assert.Equal(["Ahri.png", "Teemo.png"], watcher.Files.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ImagesOfAnUnlistedVersionAreLeftAlone()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();

        await instance.Get<IOnDemandIngestion>()
            .EnsureImagesAsync(PatchVersion.Parse("99.1.1"), [Ahri], Token);

        Assert.Empty(await harness.AssetRowsAsync("99.1.1"));
        Assert.DoesNotContain(instance.Replay.Requests, static url => Is(url, Ahri));
    }

    internal static bool Is(Uri url, DdragonImage image) =>
        url.AbsolutePath.EndsWith("/" + image.File, StringComparison.Ordinal);

    private static bool IsDataset(Uri url) =>
        url.AbsolutePath.Contains("/data/", StringComparison.Ordinal);

    // Progress<T> reports through the thread pool, after the call may have returned.
    private sealed class Watcher : IProgress<DdragonImage>
    {
        private readonly ConcurrentQueue<DdragonImage> heard = new();

        public IEnumerable<string> Files => heard.Select(static image => image.File);

        public void Report(DdragonImage value) => heard.Enqueue(value);
    }
}
