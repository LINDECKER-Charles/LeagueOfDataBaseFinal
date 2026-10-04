using System.Diagnostics.Metrics;
using System.Globalization;
using System.Threading.Channels;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Pipeline;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace LoDb.Ingestion.Tests.Queue;

/// <summary>
/// The version backlog holds a version once until its run ends, refuses beyond its capacity,
/// and ingests one version after the other, a failed run never stopping the next.
/// </summary>
public sealed class VersionBacklogTests : IDisposable
{
    private const int Capacity = 32;

    private readonly ServiceProvider provider =
        new ServiceCollection().AddMetrics().BuildServiceProvider();

    private readonly FakeLogger<VersionBacklog> logger = new();
    private readonly ScriptedIngestion ingestion = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IMeterFactory Meters => provider.GetRequiredService<IMeterFactory>();

    [Fact]
    public void VersionAlreadyWaitingIsQueuedOnce()
    {
        var backlog = CreateBacklog();

        var first = backlog.TryEnqueue(Version(1));
        var again = backlog.TryEnqueue(Version(1));

        Assert.True(first);
        Assert.True(again);
        Assert.Equal(1, backlog.Count);
    }

    [Fact]
    public void FullBacklogRefusesAVersion()
    {
        var backlog = CreateBacklog();
        var accepted = Enumerable.Range(1, Capacity)
            .Count(minor => backlog.TryEnqueue(Version(minor)));

        var refused = backlog.TryEnqueue(Version(Capacity + 1));

        Assert.Equal(Capacity, accepted);
        Assert.False(refused);
        Assert.Equal(Capacity, backlog.Count);
    }

    [Fact]
    public void DepthIsPublished()
    {
        using var depth = new MetricCollector<int>(
            Meters,
            IngestionMetrics.MeterName,
            "lodb.ingestion.queue.depth");
        var backlog = CreateBacklog();
        backlog.TryEnqueue(Version(1));
        backlog.TryEnqueue(Version(2));

        depth.RecordObservableInstruments();

        var measurement = Assert.Single(depth.GetMeasurementSnapshot());
        Assert.Equal(2, measurement.Value);
        Assert.Equal(VersionBacklog.QueueName, measurement.Tags[IngestionMetrics.QueueTag]);
    }

    [Fact]
    public async Task VersionsRunInTurnThenAreForgotten()
    {
        var backlog = CreateBacklog();
        PatchVersion[] queued = [Version(3), Version(1), Version(2)];
        Assert.All(queued, version => Assert.True(backlog.TryEnqueue(version)));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var consuming = backlog.ConsumeAsync(stop.Token);

        var runs = await ingestion.TakeAsync(queued.Length);
        await Eventually.UntilAsync(() => backlog.Count == 0, Token);
        var requeued = backlog.TryEnqueue(Version(1));
        var rerun = await ingestion.TakeAsync(1);
        await stop.CancelAsync();

        Assert.Equal(queued, runs);
        Assert.True(requeued);
        Assert.Equal([Version(1)], rerun);
        Assert.All(
            ingestion.Requests,
            static request => Assert.Same(IngestionRequest.Complete, request));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consuming);
    }

    [Fact]
    public async Task VersionStaysCountedWhileItRuns()
    {
        var backlog = CreateBacklog();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ingestion.Behaviour = (_, _) => release.Task;
        backlog.TryEnqueue(Version(1));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var consuming = backlog.ConsumeAsync(stop.Token);

        await ingestion.TakeAsync(1);
        var whileRunning = backlog.TryEnqueue(Version(1));
        var countWhileRunning = backlog.Count;
        release.SetResult();
        await Eventually.UntilAsync(() => backlog.Count == 0, Token);
        await stop.CancelAsync();

        Assert.True(whileRunning);
        Assert.Equal(1, countWhileRunning);
        Assert.Equal(1, ingestion.RunCount);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consuming);
    }

    [Fact]
    public async Task FailedRunIsLoggedAndTheNextOneStillRuns()
    {
        var backlog = CreateBacklog();
        ingestion.Behaviour = static (version, _) => version == Version(1)
            ? Task.FromException(new InvalidOperationException("Broken run."))
            : Task.CompletedTask;
        backlog.TryEnqueue(Version(1));
        backlog.TryEnqueue(Version(2));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var consuming = backlog.ConsumeAsync(stop.Token);

        var runs = await ingestion.TakeAsync(2);
        await Eventually.UntilAsync(() => backlog.Count == 0, Token);
        await stop.CancelAsync();

        Assert.Equal([Version(1), Version(2)], runs);
        var failure = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal("ingest.version.failed", failure.Id.Name);
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.IsType<InvalidOperationException>(failure.Exception);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consuming);
    }

    public void Dispose() => provider.Dispose();

    private static PatchVersion Version(int minor) =>
        PatchVersion.Parse(string.Create(CultureInfo.InvariantCulture, $"1.{minor}.1"));

    private VersionBacklog CreateBacklog() =>
        new(ingestion, new IngestionMetrics(Meters), logger);

    /// <summary>Records the versions it is asked for, then does what the test scripts.</summary>
    private sealed class ScriptedIngestion : IVersionIngestion
    {
        private readonly Channel<PatchVersion> runs = Channel.CreateUnbounded<PatchVersion>();
        private readonly List<IngestionRequest> requests = [];
        private int runCount;

        public Func<PatchVersion, CancellationToken, Task> Behaviour { get; set; } =
            static (_, _) => Task.CompletedTask;

        public int RunCount => Volatile.Read(ref runCount);

        public IReadOnlyList<IngestionRequest> Requests
        {
            get
            {
                lock (requests)
                {
                    return [.. requests];
                }
            }
        }

        public async Task<VersionIngestionResult> IngestAsync(
            PatchVersion version,
            IngestionRequest request,
            CancellationToken cancellationToken)
        {
            lock (requests)
            {
                requests.Add(request);
            }

            Interlocked.Increment(ref runCount);
            runs.Writer.TryWrite(version);
            await Behaviour(version, cancellationToken);
            return new VersionIngestionResult
            {
                Version = version,
                Outcome = VersionIngestionOutcome.Completed,
            };
        }

        /// <summary>The next <paramref name="count"/> versions run, in their order.</summary>
        public async Task<List<PatchVersion>> TakeAsync(int count)
        {
            List<PatchVersion> taken = [];
            while (taken.Count < count)
            {
                taken.Add(await runs.Reader.ReadAsync(Token));
            }

            return taken;
        }
    }
}
