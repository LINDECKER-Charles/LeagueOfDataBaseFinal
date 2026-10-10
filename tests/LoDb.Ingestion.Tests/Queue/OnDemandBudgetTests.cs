using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Pipeline;
using LoDb.Ingestion.Queue;
using LoDb.Ingestion.Tests.Pipeline;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using static LoDb.Ingestion.Tests.Queue.OnDemandIngestionTests;

namespace LoDb.Ingestion.Tests.Queue;

/// <summary>
/// The budgets of the visitors' cold work: what a sweep may make the instance fetch per
/// minute, datasets and images apart; what is stored costs nothing, work in flight is paid
/// once whatever its readers, and the worker's queued work was admitted when queued.
/// </summary>
public sealed class OnDemandBudgetTests(PostgresContainerFixture postgres)
{
    // Readers of one cold catalog at once, as a page and its API calls.
    private const int Readers = 8;

    private static readonly PatchVersion Latest = DdragonFixtures.Latest;

    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task VisitorDatasetsStopAtTheBudgetUntilTheNextMinute()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            Setting("ColdDatasetsPerMinute", "1"));
        var instance = harness.StartInstance();
        var ingestion = instance.Get<IOnDemandIngestion>();
        using var refusals = VersionIngestionTests.Collect(
            instance, "lodb.ingestion.on_demand.refused");

        Assert.True(await ingestion.EnsureDatasetsAsync(Latest, French, Token));
        var refused = await Assert.ThrowsAsync<OnDemandRefusedException>(
            () => ingestion.EnsureDatasetsAsync(Latest, DdragonLanguage.EnUs, Token));
        Assert.True(await ingestion.EnsureDatasetsAsync(Latest, French, Token));
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await ingestion.EnsureDatasetsAsync(Latest, DdragonLanguage.EnUs, Token));

        Assert.Equal(OnDemandIngestion.DatasetBudgetReason, refused.Reason);
        var refusal = Assert.Single(refusals.GetMeasurementSnapshot());
        Assert.Equal(
            OnDemandIngestion.DatasetBudgetReason,
            refusal.Tags[IngestionMetrics.ReasonTag]);
    }

    [Fact]
    public async Task ConcurrentReadersOfAColdCatalogPayItsWorkOnce()
    {
        // fr_FR and the en_US it reads over: two units of work, whatever the readers.
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            Setting("ColdDatasetsPerMinute", "2"));
        var instance = harness.StartInstance();
        using var refusals = VersionIngestionTests.Collect(
            instance, "lodb.ingestion.on_demand.refused");
        var reader = instance.Get<ICatalogReader>();

        var loads = await Task.WhenAll(Enumerable.Range(0, Readers).Select(_ =>
            reader.GetAsync(Latest, French, ColdDemand.Synchronous, Token)));

        Assert.All(loads, static load => Assert.True(load.IsReady));
        Assert.Empty(refusals.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task VisitorImagesBeyondTheBudgetStayWithoutVerdict()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            Setting("ColdImagesPerMinute", "2"));
        var ingestion = harness.StartInstance().Get<IOnDemandIngestion>();

        var refused = await Assert.ThrowsAsync<OnDemandRefusedException>(
            () => ingestion.EnsureImagesAsync(Latest, [Ahri, Garen, Teemo], Token));

        Assert.Equal(OnDemandIngestion.ImageBudgetReason, refused.Reason);
        Assert.Equal(2, (await harness.AssetRowsAsync(Latest.Value)).Count);
    }

    [Fact]
    public async Task ColdCatalogOverTheBudgetIsPendingAndRefused()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            Setting("ColdDatasetsPerMinute", "0"));
        var instance = harness.StartInstance();

        var load = await instance.Get<ICatalogReader>()
            .GetAsync(Latest, French, ColdDemand.Synchronous, Token);

        Assert.Equal((CatalogLoadStatus.Pending, true), (load.Status, load.Refused));
        Assert.Empty(harness.FilesUnder("data"));
    }

    [Fact]
    public async Task ResolutionOverTheBudgetShowsWhatLandedAndIsRefused()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            Setting("ColdImagesPerMinute", "1"));
        var resolver = harness.StartInstance().Get<IImageResolver>();

        var resolution = await resolver
            .ResolveAsync(Latest, [Ahri, Garen], ColdDemand.Synchronous, Token);

        Assert.True(resolution.Refused);
        Assert.Equal(
            [ImageStatus.Present, ImageStatus.Pending],
            new[] { resolution[Ahri].Status, resolution[Garen].Status }.Order());
    }

    [Fact]
    public async Task QueuedWorkIsNotBudgetedAgain()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            Setting("ColdImagesPerMinute", "0"));
        var instance = harness.StartInstance();
        Assert.True(instance.Get<IOnDemandIngestion>().TryEnqueue(new OnDemandRequest
        {
            Version = Latest,
            Images = [Ahri],
        }));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var consuming = instance.Get<IOnDemandBacklog>().ConsumeAsync(stop.Token);
        await Eventually.UntilAsync(
            async () => (await harness.AssetRowsAsync(Latest.Value)).Count == 1,
            Token);
        await stop.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consuming);
    }

    private static Dictionary<string, string?> Setting(string key, string value) =>
        new() { [$"{IngestionOptions.SectionName}:{key}"] = value };
}
