using LoDb.Domain.Versions;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// The run that promotes a version tells every listener once; a listener that throws is
/// logged and leaves the version promoted, the other listeners told.
/// </summary>
public sealed class PromotionListenerTests(PostgresContainerFixture postgres)
{
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

    private sealed class RecordingListener : IPromotionListener
    {
        public List<PatchVersion> Promoted { get; } = [];

        public Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken)
        {
            Promoted.Add(version);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingListener : IPromotionListener
    {
        public Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken) =>
            throw new HttpRequestException("The endpoint is unreachable.");
    }
}
