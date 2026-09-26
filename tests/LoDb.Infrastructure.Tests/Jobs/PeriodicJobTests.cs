using System.Diagnostics.Metrics;
using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Locks;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Infrastructure.Tests.Jobs;

/// <summary>
/// A periodic job runs once per period whatever the number of instances, never twice at
/// once, and a failure is logged, measured and retried at the next period.
/// </summary>
public sealed class PeriodicJobTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string JobName = "test-job";
    private static readonly TimeSpan Period = TimeSpan.FromHours(1);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider _time =
        new(new DateTimeOffset(2026, 9, 26, 8, 0, 0, TimeSpan.Zero));

    private readonly FakeLoggerProvider _logs = new();
    private readonly List<PostgresDistributedLock> _locks = [];
    private readonly List<TestJob> _jobs = [];
    private ServiceProvider? _host;
    private JobMetrics? _metrics;

    private ServiceProvider Host => _host ??= new ServiceCollection()
        .AddLogging(logging => logging.AddProvider(_logs))
        .AddMetrics()
        .BuildServiceProvider();

    private IMeterFactory MeterFactory => Host.GetRequiredService<IMeterFactory>();

    private JobMetrics Metrics => _metrics ??= new JobMetrics(MeterFactory);

    [Fact]
    public async Task JobRunsOnceAcrossTwoInstances()
    {
        var first = NewJob();
        var second = NewJob();
        first.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var running = first.RunIfDueAsync(Cancellation);
        await first.Entered.Task.WaitAsync(Patience, Cancellation);
        var whileRunning = await second.RunIfDueAsync(Cancellation);
        first.Gate.SetResult();
        var firstRun = await running;
        var afterRun = await second.RunIfDueAsync(Cancellation);
        _time.Advance(Period);
        var nextPeriod = await Task.WhenAll(
            first.RunIfDueAsync(Cancellation),
            second.RunIfDueAsync(Cancellation));

        Assert.Equal(JobRunOutcome.Locked, whileRunning);
        Assert.Equal(JobRunOutcome.Succeeded, firstRun);
        Assert.Equal(JobRunOutcome.NotDue, afterRun);
        Assert.Single(nextPeriod, static outcome => outcome is JobRunOutcome.Succeeded);
        Assert.DoesNotContain(JobRunOutcome.Failed, nextPeriod);
        Assert.Equal(2, first.Runs + second.Runs);
        Assert.Equal([Stamp(_time.GetUtcNow()), Stamp(_time.GetUtcNow())], await ScheduleAsync());
    }

    [Theory]
    [InlineData(3600, 60, JobRunOutcome.Succeeded)]
    [InlineData(3600, 61, JobRunOutcome.NotDue)]
    [InlineData(300, 30, JobRunOutcome.Succeeded)]
    [InlineData(300, 31, JobRunOutcome.NotDue)]
    public async Task TickSlightlyEarlyStillRuns(
        int periodSeconds,
        int earlySeconds,
        JobRunOutcome expected)
    {
        var job = NewJob(period: TimeSpan.FromSeconds(periodSeconds));
        await job.RunIfDueAsync(Cancellation);

        _time.Advance(TimeSpan.FromSeconds(periodSeconds - earlySeconds));
        var outcome = await job.RunIfDueAsync(Cancellation);

        Assert.Equal(expected, outcome);
    }

    [Fact]
    public async Task FailureIsLoggedMeasuredAndRetriedNextPeriod()
    {
        using var failures = Collect<long>("lodb.job.failures");
        using var durations = Collect<double>("lodb.job.duration");
        using var lastSuccess = Collect<long>("lodb.job.last_success");
        var job = NewJob();
        var failure = new InvalidOperationException("Data Dragon is down.");
        job.Failure = failure;

        var failed = await job.RunIfDueAsync(Cancellation);
        var afterFailure = await ScheduleAsync();
        var tooSoon = await job.RunIfDueAsync(Cancellation);
        job.Failure = null;
        _time.Advance(Period);
        var retried = await job.RunIfDueAsync(Cancellation);
        lastSuccess.RecordObservableInstruments();

        Assert.Equal(JobRunOutcome.Failed, failed);
        Assert.Equal([Stamp(_time.GetUtcNow() - Period), "NULL"], afterFailure);
        Assert.Equal(JobRunOutcome.NotDue, tooSoon);
        Assert.Equal(JobRunOutcome.Succeeded, retried);
        Assert.Equal([Stamp(_time.GetUtcNow()), Stamp(_time.GetUtcNow())], await ScheduleAsync());

        var failureCount = Assert.Single(failures.GetMeasurementSnapshot());
        Assert.Equal(1, failureCount.Value);
        Assert.Equal(JobName, failureCount.Tags[JobMetrics.JobTag]);
        Assert.Equal(
            ["failed", "succeeded"],
            durations.GetMeasurementSnapshot().Select(static m => m.Tags[JobMetrics.OutcomeTag]));
        Assert.Equal(_time.GetUtcNow().ToUnixTimeSeconds(), lastSuccess.LastMeasurement?.Value);

        var logs = _logs.Collector.GetSnapshot();
        var error = Assert.Single(logs, static log => log.Id.Name == "jobs.run.failed");
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Same(failure, error.Exception);
        Assert.Equal($"Job {JobName} failed after 0 ms.", error.Message);
        var success = Assert.Single(logs, static log => log.Id.Name == "jobs.run.succeeded");
        Assert.Equal(LogLevel.Information, success.Level);
        Assert.Equal($"Job {JobName} processed 3 versions in 0 ms.", success.Message);
    }

    [Fact]
    public async Task UnreachableLockIsAFailureNotACrash()
    {
        using var failures = Collect<long>("lodb.job.failures");
        var unreachable = new PostgresDistributedLock(
            "Host=127.0.0.1;Port=1;Username=nobody;Password=none;Database=none;Timeout=2");
        _locks.Add(unreachable);
        var job = Track(new TestJob(Services(unreachable), JobName, Period));

        var outcome = await job.RunIfDueAsync(Cancellation);

        Assert.Equal(JobRunOutcome.Failed, outcome);
        Assert.Equal(0, job.Runs);
        Assert.Equal(1, Assert.Single(failures.GetMeasurementSnapshot()).Value);
        var warning = Assert.Single(
            _logs.Collector.GetSnapshot(),
            static log => log.Id.Name == "jobs.schedule.failed");
        Assert.Equal(LogLevel.Warning, warning.Level);
    }

    [Fact]
    public async Task ExecutionRunsAtStartThenEveryPeriod()
    {
        var job = NewJob();

        await job.StartAsync(Cancellation);
        Assert.True(await job.Completed.WaitAsync(Patience, Cancellation));
        _time.Advance(Period);
        Assert.True(await job.Completed.WaitAsync(Patience, Cancellation));
        _time.Advance(Period);
        Assert.True(await job.Completed.WaitAsync(Patience, Cancellation));
        await job.StopAsync(Cancellation);

        Assert.Equal(3, job.Runs);
    }

    [Theory]
    [InlineData("", 3600)]
    [InlineData(" ", 3600)]
    [InlineData("a-name-longer-than-the-sixty-four-characters-of-periodic-job-name", 3600)]
    [InlineData(JobName, 0)]
    [InlineData(JobName, -1)]
    public async Task InvalidJobStopsAtStart(string name, int periodSeconds)
    {
        var job = NewJob(name, TimeSpan.FromSeconds(periodSeconds));

        await job.StartAsync(Cancellation);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteTask!);

        Assert.StartsWith(
            $"Job {nameof(TestJob)} needs a name",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(0, job.Runs);
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        foreach (var job in _jobs)
        {
            job.Dispose();
        }

        foreach (var instance in _locks)
        {
            await instance.DisposeAsync();
        }

        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        _logs.Dispose();
    }

    private static string Stamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss+00", null);

    private MetricCollector<T> Collect<T>(string instrument)
        where T : struct =>
        new(MeterFactory, JobMetrics.MeterName, instrument);

    // One API instance: its own lock connection, the shared database and clock.
    private TestJob NewJob(string name = JobName, TimeSpan? period = null)
    {
        var instanceLock = new PostgresDistributedLock(Database.ConnectionString);
        _locks.Add(instanceLock);
        return Track(new TestJob(Services(instanceLock), name, period ?? Period));
    }

    private PeriodicJobServices Services(IDistributedLock distributedLock) =>
        new(
            distributedLock,
            new PostgresJobSchedule(Database.DataSource),
            Metrics,
            _time,
            Host.GetRequiredService<ILoggerFactory>());

    private TestJob Track(TestJob job)
    {
        _jobs.Add(job);
        return job;
    }

    private Task<IReadOnlyList<string>> ScheduleAsync() =>
        Database.QueryAsync(
            $"""
            SELECT unnest(ARRAY[last_started_at::text, last_succeeded_at::text])
            FROM periodic_job WHERE name = '{JobName}'
            """,
            Cancellation);

    private sealed class TestJob(PeriodicJobServices services, string name, TimeSpan period)
        : PeriodicJob(services)
    {
        private int _runs;

        public override string Name => name;

        public override TimeSpan Period => period;

        public int Runs => Volatile.Read(ref _runs);

        /// <summary>A run waits for it before finishing; open unless a test closes it.</summary>
        public TaskCompletionSource Gate { get; set; } = Open();

        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Released once per finished run.</summary>
        public SemaphoreSlim Completed { get; } = new(0);

        public Exception? Failure { get; set; }

        public override void Dispose()
        {
            Completed.Dispose();
            base.Dispose();
        }

        protected override async Task<JobRunSummary> RunOnceAsync(
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _runs);
            Entered.TrySetResult();
            try
            {
                await Gate.Task.WaitAsync(cancellationToken);
                return Failure is { } failure ? throw failure : new JobRunSummary(3, "versions");
            }
            finally
            {
                Completed.Release();
            }
        }

        private static TaskCompletionSource Open()
        {
            var gate = new TaskCompletionSource();
            gate.SetResult();
            return gate;
        }
    }
}
