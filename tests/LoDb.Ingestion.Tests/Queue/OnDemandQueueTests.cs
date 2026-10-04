using System.Net;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Pipeline;
using LoDb.Ingestion.Queue;
using LoDb.Ingestion.Tests.Pipeline;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Tests.Queue;

/// <summary>
/// The queued path of the long tail: bounded and without duplicates, crawlers held to their
/// budget (C5), and consumed by the worker in the background.
/// </summary>
public sealed class OnDemandQueueTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Latest = DdragonFixtures.Latest;

    private static readonly DdragonImage Fiddlesticks =
        new(DdragonImageKind.Champion, "Fiddlesticks.png");

    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task FullQueueRefusesNewWork()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            new Dictionary<string, string?> { ["LoDb:Ingestion:QueueCapacity"] = "2" });
        var instance = harness.StartInstance();
        var ingestion = instance.Get<IOnDemandIngestion>();
        using var refusals = CollectRefusals(instance);

        var first = ingestion.TryEnqueue(Visitor(OnDemandIngestionTests.Ahri));
        var again = ingestion.TryEnqueue(Visitor(OnDemandIngestionTests.Ahri));
        var second = ingestion.TryEnqueue(Visitor(OnDemandIngestionTests.Garen));
        var third = ingestion.TryEnqueue(Visitor(OnDemandIngestionTests.Teemo));

        Assert.True(first);
        Assert.True(again);
        Assert.True(second);
        Assert.False(third);
        Assert.Equal(2, instance.Get<IOnDemandBacklog>().Count);
        var refusal = Assert.Single(refusals.GetMeasurementSnapshot());
        Assert.Equal(OnDemandIngestion.FullReason, refusal.Tags[IngestionMetrics.ReasonTag]);
    }

    [Fact]
    public async Task CrawlersSpendTheirBudgetAndVisitorsDoNot()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            new Dictionary<string, string?>
            {
                ["LoDb:Ingestion:CrawlerRequestsPerMinute"] = "2",
            });
        var instance = harness.StartInstance();
        var ingestion = instance.Get<IOnDemandIngestion>();
        using var refusals = CollectRefusals(instance);

        var first = ingestion.TryEnqueue(Crawler(OnDemandIngestionTests.Ahri));
        var second = ingestion.TryEnqueue(Crawler(OnDemandIngestionTests.Garen));
        var waiting = ingestion.TryEnqueue(Crawler(OnDemandIngestionTests.Ahri));
        var spent = ingestion.TryEnqueue(Crawler(OnDemandIngestionTests.Teemo));
        var visitor = ingestion.TryEnqueue(Visitor(OnDemandIngestionTests.Teemo));
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        var refilled = ingestion.TryEnqueue(Crawler(Fiddlesticks));

        Assert.Equal(
            [true, true, true, false, true, true],
            [first, second, waiting, spent, visitor, refilled]);
        var refusal = Assert.Single(refusals.GetMeasurementSnapshot());
        Assert.Equal(
            OnDemandIngestion.CrawlerBudgetReason,
            refusal.Tags[IngestionMetrics.ReasonTag]);
        Assert.Equal(4, instance.Get<IOnDemandBacklog>().Count);
    }

    [Fact]
    public async Task WorkerSettlesQueuedWork()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var backlog = instance.Get<IOnDemandBacklog>();
        var request = Visitor(OnDemandIngestionTests.Ahri) with
        {
            Language = OnDemandIngestionTests.French,
        };
        Assert.True(instance.Get<IOnDemandIngestion>().TryEnqueue(request));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var consuming = backlog.ConsumeAsync(stop.Token);
        await Eventually.UntilAsync(
            async () => (await harness.AssetRowsAsync(Latest.Value)).Count == 1,
            Token);
        await stop.CancelAsync();

        Assert.Equal(4, harness.FilesUnder($"data/{Latest.Value}/fr_FR").Count);
        Assert.Equal(0, backlog.Count);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consuming);
    }

    [Fact]
    public async Task FailedWorkIsLoggedAndTheWorkerGoesOn()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        // One dataset read and its three retries.
        instance.Upstream.FailNext(
            static url => url.AbsolutePath.EndsWith("/fr_FR/item.json", StringComparison.Ordinal),
            count: 4,
            HttpStatusCode.ServiceUnavailable);
        var ingestion = instance.Get<IOnDemandIngestion>();
        ingestion.TryEnqueue(new OnDemandRequest
        {
            Version = Latest,
            Language = OnDemandIngestionTests.French,
        });
        ingestion.TryEnqueue(Visitor(OnDemandIngestionTests.Ahri));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var consuming = instance.Get<IOnDemandBacklog>().ConsumeAsync(stop.Token);
        await Eventually.UntilAsync(
            async () => (await harness.AssetRowsAsync(Latest.Value)).Count == 1,
            Token);
        await stop.CancelAsync();

        var failure = Assert.Single(
            instance.Logs,
            static log => log.Id.Name == "ingest.on_demand.failed");
        Assert.Equal(LogLevel.Warning, failure.Level);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consuming);
    }

    private static MetricCollector<long> CollectRefusals(IngestionInstance instance) =>
        VersionIngestionTests.Collect(instance, "lodb.ingestion.on_demand.refused");

    private static OnDemandRequest Visitor(DdragonImage image) =>
        new() { Version = Latest, Images = [image], Origin = OnDemandOrigin.Visitor };

    private static OnDemandRequest Crawler(DdragonImage image) =>
        new() { Version = Latest, Images = [image], Origin = OnDemandOrigin.Crawler };
}
