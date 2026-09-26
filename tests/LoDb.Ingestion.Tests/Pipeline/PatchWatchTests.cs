using System.Net;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Pipeline.Watch;
using LoDb.Ingestion.Queue;
using LoDb.Testing;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// The patch watch records the versions newer than every known one and queues the due ones;
/// a failed read of <c>versions.json</c> records and caches nothing.
/// </summary>
public sealed class PatchWatchTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task FirstWatchDiscoversTheNewestVersionOnly()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();

        var queued = await instance.Get<IPatchWatch>().WatchAsync(Token);

        Assert.Equal(1, queued);
        var row = Assert.Single(await instance.Get<IDdragonVersionStore>().ListAsync(Token));
        Assert.Equal("16.19.1", row.Version);
        Assert.Equal(DdragonVersionStatus.Discovered, row.Status);
        Assert.Equal(0, row.Attempts);
        Assert.Equal(IngestionHarness.Start, row.DiscoveredAt);
        Assert.Equal(1, instance.Get<IVersionBacklog>().Count);
        Assert.Single(instance.Logs, static log => log.Id.Name == "ingest.version.discovered");
    }

    [Fact]
    public async Task WatchDiscoversEveryVersionNewerThanTheKnownOnes()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        await SeedAsync(harness, "7.22.1", DdragonVersionStatus.Ready, nextAttemptAt: null);
        var instance = harness.StartInstance();

        var queued = await instance.Get<IPatchWatch>().WatchAsync(Token);

        Assert.Equal(3, queued);
        var rows = await instance.Get<IDdragonVersionStore>().ListAsync(Token);
        Assert.Equal(
            ["16.18.1", "16.19.1", "7.22.1", "8.7.1"],
            rows.Select(static row => row.Version).Order(StringComparer.Ordinal));
        Assert.Equal(3, instance.Get<IVersionBacklog>().Count);
    }

    [Fact]
    public async Task WatchQueuesAVersionOnceItsRetryIsDue()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var retry = IngestionHarness.Start + TimeSpan.FromMinutes(5);
        await SeedAsync(harness, "16.19.1", DdragonVersionStatus.Discovered, retry);
        var instance = harness.StartInstance();
        var watch = instance.Get<IPatchWatch>();

        var early = await watch.WatchAsync(Token);
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        var due = await watch.WatchAsync(Token);

        Assert.Equal(0, early);
        Assert.Equal(1, due);
        Assert.Equal(1, instance.Get<IVersionBacklog>().Count);
    }

    [Fact]
    public async Task FailedVersionListIsNeitherRecordedNorCached()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        var watch = instance.Get<IPatchWatch>();
        // The first request and its three retries.
        instance.Upstream.FailNext(
            static url => url.AbsolutePath == "/api/versions.json",
            count: 4,
            HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAnyAsync<EgressException>(() => watch.WatchAsync(Token));
        var afterFailure = await instance.Get<IDdragonVersionStore>().ListAsync(Token);
        var queued = await watch.WatchAsync(Token);

        Assert.Empty(afterFailure);
        Assert.Equal(1, queued);
        var row = Assert.Single(await instance.Get<IDdragonVersionStore>().ListAsync(Token));
        Assert.Equal("16.19.1", row.Version);
    }

    private static async Task SeedAsync(
        IngestionHarness harness,
        string version,
        DdragonVersionStatus status,
        DateTimeOffset? nextAttemptAt)
    {
        var earlier = IngestionHarness.Start - TimeSpan.FromDays(1);
        await using var db = harness.Database.CreateContext();
        db.DdragonVersions.Add(new DdragonVersion
        {
            Version = version,
            Status = status,
            Attempts = 1,
            NextAttemptAt = nextAttemptAt,
            DiscoveredAt = earlier,
            UpdatedAt = earlier,
            ReadyAt = status == DdragonVersionStatus.Ready ? earlier : null,
            PromotedAt = status == DdragonVersionStatus.Ready ? earlier : null,
        });
        await db.SaveChangesAsync(Token);
    }
}
