using System.Net;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Queue;
using LoDb.Ingestion.Tests.Pipeline;
using LoDb.Ingestion.Tests.Queue;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The images of a page: a blob, a placeholder for good, or pending while the ingestion
/// catches up, fetched or queued as the caller asks and never fetched twice once settled.
/// </summary>
public sealed class ImageResolverTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Latest = DdragonFixtures.Latest;
    private static readonly DdragonImage Ahri = OnDemandIngestionTests.Ahri;
    private static readonly DdragonImage Garen = OnDemandIngestionTests.Garen;
    private static readonly DdragonImage Teemo = OnDemandIngestionTests.Teemo;

    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task ColdImagesAreFetchedForASynchronousRead()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        instance.Replay.FailWith(static url => Is(url, Garen), HttpStatusCode.Forbidden);

        var images = await ResolveAsync(instance, ColdDemand.Synchronous, Ahri, Garen, Ahri);

        var ahri = images[Ahri];
        var sha = (await harness.AssetRowsAsync(Latest.Value))
            .Single(static row => row.Key == Ahri.File).Sha256;
        Assert.Equal(ImageStatus.Present, ahri.Status);
        Assert.Equal($"/cdn/blobs/{sha}.png", ahri.Url);
        Assert.Equal($"/cdn/blobs/{sha}.webp", ahri.WebpUrl);
        Assert.Equal(ResolvedImage.Absent, images[Garen]);
        Assert.False(images.HasPending);
        Assert.Single(instance.Replay.Requests, static url => Is(url, Ahri));
    }

    [Fact]
    public async Task SettledImagesAreReadFromTheManifestAlone()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var first = harness.StartInstance();
        first.Replay.FailWith(static url => Is(url, Garen), HttpStatusCode.Forbidden);
        var settled = await ResolveAsync(first, ColdDemand.Synchronous, Ahri, Garen);
        var instance = harness.StartInstance();

        var images = await ResolveAsync(instance, ColdDemand.StoredOnly, Ahri, Garen);

        Assert.Equal(settled[Ahri], images[Ahri]);
        Assert.Equal(ResolvedImage.Absent, images[Garen]);
        Assert.Empty(instance.Replay.Requests);
    }

    [Fact]
    public async Task QueuedReadLeavesColdImagesPending()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();

        var images = await ResolveAsync(instance, ColdDemand.Queued, Ahri, Garen);

        Assert.Equal(ResolvedImage.Pending, images[Ahri]);
        Assert.True(images.HasPending);
        Assert.False(images.Refused);
        Assert.Equal(1, instance.Get<IOnDemandBacklog>().Count);
        Assert.Empty(instance.Replay.Requests);
    }

    [Fact]
    public async Task CrawlerReadIsQueuedWithinItsBudget()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            new Dictionary<string, string?> { ["LoDb:Ingestion:CrawlerRequestsPerMinute"] = "1" });
        var instance = harness.StartInstance();
        var crawler = ColdDemand.Synchronous.For(OnDemandOrigin.Crawler);

        var first = await ResolveAsync(instance, crawler, Ahri);
        var spent = await ResolveAsync(instance, crawler, Garen);

        Assert.Equal(ResolvedImage.Pending, first[Ahri]);
        Assert.False(first.Refused);
        Assert.True(spent.Refused);
        Assert.Equal(1, instance.Get<IOnDemandBacklog>().Count);
        Assert.Empty(instance.Replay.Requests);
    }

    [Fact]
    public async Task StoredOnlyReadQueuesNothing()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();

        var images = await ResolveAsync(instance, ColdDemand.StoredOnly, Ahri);

        Assert.Equal(ResolvedImage.Pending, images[Ahri]);
        Assert.False(images.Refused);
        Assert.Equal(0, instance.Get<IOnDemandBacklog>().Count);
        Assert.Empty(instance.Replay.Requests);
        Assert.Throws<KeyNotFoundException>(() => images[Garen]);
    }

    [Fact]
    public async Task TransientFailureIsLeftPendingThenFetchedAgain()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        // The first request and its three retries.
        instance.Upstream.FailNext(
            static url => Is(url, Teemo),
            count: 4,
            HttpStatusCode.ServiceUnavailable);

        var failed = await ResolveAsync(instance, ColdDemand.Synchronous, Teemo);
        var recorded = await harness.AssetRowsAsync(Latest.Value);
        var retried = await ResolveAsync(instance, ColdDemand.Synchronous, Teemo);

        Assert.Equal(ResolvedImage.Pending, failed[Teemo]);
        Assert.Empty(recorded);
        Assert.Equal(ImageStatus.Present, retried[Teemo].Status);
        // The failed attempts never reach the recording: only the retried fetch does.
        Assert.Single(instance.Replay.Requests, static url => Is(url, Teemo));
    }

    [Fact]
    public async Task UpstreamFailureLeavesImagesPending()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        instance.Upstream.FailNext(
            static url => url.AbsolutePath == "/api/versions.json",
            count: 4,
            HttpStatusCode.ServiceUnavailable);

        var images = await ResolveAsync(instance, ColdDemand.Synchronous, Ahri);

        Assert.Equal(ResolvedImage.Pending, images[Ahri]);
        var failure = Assert.Single(
            instance.Logs,
            static log => log.Id.Name == "catalog.images.failed");
        Assert.Equal(LogLevel.Warning, failure.Level);
        Assert.Empty(await harness.AssetRowsAsync(Latest.Value));
    }

    private static Task<ImageResolution> ResolveAsync(
        IngestionInstance instance,
        ColdDemand demand,
        params DdragonImage[] images) =>
        instance.Get<IImageResolver>().ResolveAsync(Latest, images, demand, Token);

    private static bool Is(Uri url, DdragonImage image) => OnDemandIngestionTests.Is(url, image);
}
