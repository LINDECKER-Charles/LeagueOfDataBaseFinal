using System.Diagnostics.Metrics;
using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Locks;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Jobs;

/// <summary>
/// A job without a usable name or period stops at start, before it takes a lock or reads the
/// schedule: its services point at a database that never answers.
/// </summary>
public sealed class PeriodicJobDefinitionTests
{
    // One character past the 64 a lock key, a row and a metric tag allow.
    private const string NameOfSixtyFiveCharacters =
        "a-name-longer-than-the-sixty-four-characters-of-periodic-job-name";

    [Theory]
    [InlineData(" ", 3600)]
    [InlineData(NameOfSixtyFiveCharacters, 3600)]
    [InlineData("test-job", 0)]
    public async Task InvalidJobStopsAtStart(string name, int periodSeconds)
    {
        await using var dataSource = NpgsqlDataSource.Create(ApiFactory.UnreachablePostgres);
        await using var instanceLock = new PostgresDistributedLock(ApiFactory.UnreachablePostgres);
        using var meters = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var services = new PeriodicJobServices(
            instanceLock,
            new PostgresJobSchedule(dataSource),
            new JobMetrics(meters.GetRequiredService<IMeterFactory>()),
            TimeProvider.System,
            NullLoggerFactory.Instance);
        using var job = new NamedJob(services, name, TimeSpan.FromSeconds(periodSeconds));

        await job.StartAsync(TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteTask!);

        Assert.StartsWith(
            $"Job {nameof(NamedJob)} needs a name",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(0, job.Runs);
    }

    private sealed class NamedJob(PeriodicJobServices services, string name, TimeSpan period)
        : PeriodicJob(services)
    {
        private int _runs;

        public override string Name => name;

        public override TimeSpan Period => period;

        public int Runs => Volatile.Read(ref _runs);

        protected override Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _runs);
            return Task.FromResult(new JobRunSummary(0, "versions"));
        }
    }
}
