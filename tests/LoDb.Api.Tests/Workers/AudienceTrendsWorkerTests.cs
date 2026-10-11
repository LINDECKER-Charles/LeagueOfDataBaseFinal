using LoDb.Api.Workers.Analytics;
using LoDb.Infrastructure.Audience;
using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Workers;

/// <summary>
/// The hourly pull of the audience sleeps, saying so once, until its four settings are there,
/// and runs as a periodic job of its own once they are.
/// </summary>
public sealed class AudienceTrendsWorkerTests : IAsyncDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly FakeLoggerProvider _logs = new();
    private readonly ServiceProvider _host;
    private readonly CountingPull _pull = new();
    private readonly RecordingLock _lock = new();

    public AudienceTrendsWorkerTests() =>
        _host = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(_logs))
            .AddMetrics()
            .BuildServiceProvider();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithoutTheReadOnlyAccountThePullSleeps()
    {
        using var worker = Worker(new AudienceOptions { Host = "stats.example.test" });

        await worker.StartAsync(Cancellation);
        await worker.StopAsync(Cancellation);

        Assert.Null(worker.ExecuteTask);
        Assert.Equal(0, _pull.Runs);
        Assert.Equal(0, _lock.Asked);
        var line = Assert.Single(
            _logs.Collector.GetSnapshot(),
            static record => record.Id.Name == "audience.trends.dormant");
        Assert.Equal(LogLevel.Information, line.Level);
    }

    [Fact]
    public async Task ConfiguredThePullRunsAsAJobEveryPeriod()
    {
        var options = new AudienceOptions
        {
            Host = "stats.example.test",
            WebsiteId = Guid.Parse("deee1958-0f6c-4a32-a327-aad389836c3b"),
            Username = "trends-reader",
            Password = "secret",
            PullPeriod = TimeSpan.FromMinutes(30),
        };
        using var worker = Worker(options);

        await worker.StartAsync(Cancellation);
        await _lock.FirstAsk.Task.WaitAsync(Patience, Cancellation);
        await worker.StopAsync(Cancellation);

        Assert.NotNull(worker.ExecuteTask);
        Assert.Equal("job:audience.trends", _lock.Name);
        Assert.Equal(TimeSpan.FromMinutes(30), worker.Period);
        Assert.DoesNotContain(
            _logs.Collector.GetSnapshot(),
            static record => record.Id.Name == "audience.trends.dormant");
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        _logs.Dispose();
    }

    private AudienceTrendsWorker Worker(AudienceOptions options)
    {
        var services = new PeriodicJobServices(
            _lock,
            new NeverDueSchedule(),
            new JobMetrics(_host.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>()),
            new FakeTimeProvider(),
            _host.GetRequiredService<ILoggerFactory>());
        return new AudienceTrendsWorker(
            services,
            _pull,
            Options.Create(options),
            _host.GetRequiredService<ILogger<AudienceTrendsWorker>>());
    }

    private sealed class CountingPull : IAudiencePull
    {
        public int Runs { get; private set; }

        public Task<long> PullAsync(CancellationToken cancellationToken)
        {
            Runs++;
            return Task.FromResult(0L);
        }
    }

    // Held elsewhere: the tick stops there, after asking.
    private sealed class RecordingLock : IDistributedLock
    {
        public TaskCompletionSource FirstAsk { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Asked { get; private set; }

        public string? Name { get; private set; }

        public Task<IAsyncDisposable?> TryAcquireAsync(
            string name,
            CancellationToken cancellationToken)
        {
            Asked++;
            Name = name;
            FirstAsk.TrySetResult();
            return Task.FromResult<IAsyncDisposable?>(null);
        }
    }

    private sealed class NeverDueSchedule : IJobSchedule
    {
        public Task<bool> TryStartAsync(
            string job,
            DateTimeOffset now,
            TimeSpan interval,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task RecordSuccessAsync(
            string job,
            DateTimeOffset now,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
