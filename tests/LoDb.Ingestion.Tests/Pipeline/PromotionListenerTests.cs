using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// The run that promotes a version tells every listener once, stage by stage; a listener that
/// throws is logged and leaves the version promoted, the other listeners told. The index of
/// states, the first stage, covers the version before the later stages read it.
/// </summary>
public sealed class PromotionListenerTests(PostgresContainerFixture postgres)
{
    private const PromotionStage StateIndex = PromotionStage.StateIndex;
    private const PromotionStage Prerender = PromotionStage.Prerender;
    private const PromotionStage Announce = PromotionStage.Announce;

    private static readonly PatchVersion Latest = DdragonFixtures.Latest;

    [Fact]
    public async Task PromotionTellsEveryListenerOnce()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var listener = new RecordingListener();
        var instance = harness.StartInstance(services =>
            services.AddSingleton<IPromotionListener>(listener));

        var promoted = await VersionIngestionTests.IngestAsync(instance, Latest);
        var again = await VersionIngestionTests.IngestAsync(instance, Latest);

        Assert.True(promoted.Promoted);
        Assert.False(again.Promoted);
        Assert.Equal([Latest], listener.Promoted);
    }

    [Fact]
    public async Task FailingListenerLeavesTheVersionPromoted()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var listener = new RecordingListener();
        var instance = harness.StartInstance(services => services
            .AddSingleton<IPromotionListener>(new FailingListener())
            .AddSingleton<IPromotionListener>(listener));

        var result = await VersionIngestionTests.IngestAsync(instance, Latest);

        Assert.Equal(VersionIngestionOutcome.Completed, result.Outcome);
        Assert.True(result.Promoted);
        Assert.Equal([Latest], listener.Promoted);
        var failure = Assert.Single(
            instance.Logs,
            static log => log.Id.Name == "ingest.listener.failed");
        Assert.Equal(LogLevel.Warning, failure.Level);
        Assert.IsType<HttpRequestException>(failure.Exception);
    }

    [Fact]
    public async Task ListenersRunByStageWhateverTheirRegistrationOrder()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        List<string> calls = [];
        var instance = harness.StartInstance(services => services
            .AddSingleton<IPromotionListener>(new OrderListener(Announce, "announce", calls))
            .AddSingleton<IPromotionListener>(new OrderListener(Prerender, "first", calls))
            .AddSingleton<IPromotionListener>(new OrderListener(StateIndex, "index", calls))
            .AddSingleton<IPromotionListener>(new OrderListener(Prerender, "second", calls)));

        await VersionIngestionTests.IngestAsync(instance, Latest);

        Assert.Equal(["index", "first", "second", "announce"], calls);
    }

    [Fact]
    public async Task IndexOfStatesCoversThePromotedVersionBeforeTheLaterStages()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var probe = new CoverageProbe();
        var instance = harness.StartInstance(services =>
            services.AddSingleton<IPromotionListener>(probe));
        probe.Index = instance.Get<IEntityStateIndex>();

        await VersionIngestionTests.IngestAsync(instance, Latest);

        Assert.Equal([true], probe.Covered);
        Assert.DoesNotContain(instance.Logs, static log => log.Id.Name == "ingest.listener.failed");
    }

    private sealed class RecordingListener : IPromotionListener
    {
        public List<PatchVersion> Promoted { get; } = [];

        public PromotionStage Stage => PromotionStage.Announce;

        public Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken)
        {
            Promoted.Add(version);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingListener : IPromotionListener
    {
        public PromotionStage Stage => PromotionStage.StateIndex;

        public Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken) =>
            throw new HttpRequestException("The endpoint is unreachable.");
    }

    // A pre-rendering stage: what the index it reads covers when its turn comes.
    private sealed class CoverageProbe : IPromotionListener
    {
        public IEntityStateIndex? Index { get; set; }

        public List<bool> Covered { get; } = [];

        public PromotionStage Stage => PromotionStage.Prerender;

        public async Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken)
        {
            var snapshot = await Index!.GetAsync(cancellationToken);
            Covered.Add(snapshot.Coverage.Contains(version));
        }
    }

    private sealed class OrderListener(PromotionStage stage, string name, List<string> calls)
        : IPromotionListener
    {
        public PromotionStage Stage => stage;

        public Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken)
        {
            calls.Add(name);
            return Task.CompletedTask;
        }
    }
}
