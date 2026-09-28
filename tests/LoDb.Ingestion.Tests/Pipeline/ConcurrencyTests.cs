using LoDb.Domain.Versions;
using LoDb.Infrastructure.Locks;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// Instances sharing a database ingest a version once: the version's lock keeps a second run
/// out, and nothing is fetched twice.
/// </summary>
public sealed class ConcurrencyTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Latest = DdragonFixtures.Latest;

    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task LockedVersionIsLeftToItsHolder()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var holder = harness.StartInstance();
        var runner = harness.StartInstance();
        var held = await holder.Get<IDistributedLock>()
            .TryAcquireAsync(VersionIngestion.LockName(Latest), Token);
        Assert.NotNull(held);

        VersionIngestionResult result;
        await using (held)
        {
            result = await VersionIngestionTests.IngestAsync(runner, Latest);
        }

        Assert.Equal(VersionIngestionOutcome.Locked, result.Outcome);
        Assert.Empty(runner.Replay.Requests);
        Assert.Null(await harness.VersionRowAsync(Latest.Value));
        Assert.Contains(runner.Logs, static log => log.Id.Name == "ingest.version.locked");
    }

    [Fact]
    public async Task ConcurrentInstancesIngestAVersionOnce()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        IngestionInstance[] instances = [harness.StartInstance(), harness.StartInstance()];

        var results = await Task.WhenAll(
            instances.Select(instance => VersionIngestionTests.IngestAsync(instance, Latest)));

        Assert.Contains(
            results,
            static result => result.Outcome is VersionIngestionOutcome.Completed);
        Assert.All(results, static result => Assert.True(
            result.Outcome is VersionIngestionOutcome.Completed or VersionIngestionOutcome.Locked,
            result.Outcome.ToString()));
        var fetched = instances
            .SelectMany(static instance => instance.RequestedPaths())
            .Where(static path => !VersionIngestionTests.ListDocuments.Contains(path))
            .ToList();
        Assert.Equal(fetched.Count, fetched.Distinct(StringComparer.Ordinal).Count());
        var row = await harness.VersionRowAsync(Latest.Value);
        Assert.Equal(DdragonVersionStatus.Ready, row?.Status);
        Assert.Equal(1, row?.Attempts);
        Assert.Single(results, static result => result.Promoted);
    }
}
