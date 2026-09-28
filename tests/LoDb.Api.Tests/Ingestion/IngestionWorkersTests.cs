using System.Diagnostics;
using LoDb.Api.Workers;
using LoDb.Api.Workers.Ingestion;
using LoDb.Infrastructure.Jobs;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Ingestion;

/// <summary>
/// The patch watch runs once per period and queues the newest version, which the ingestion
/// worker then ingests whole: the path of a new patch from Data Dragon to the API.
/// </summary>
public sealed class IngestionWorkersTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>
{
    private const string Promoted =
        "SELECT version FROM ddragon_version WHERE status = 'ready' AND promoted_at IS NOT NULL";

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

    private static CancellationToken Token => IngestionHost.Token;

    [Fact]
    public void ConventionRegistersTheIngestionWorkers()
    {
        var workers = ConventionalWorkers.Discover(typeof(Program).Assembly);

        Assert.Contains(typeof(PatchWatchJob), workers);
        Assert.Contains(typeof(VersionIngestionWorker), workers);
        Assert.Contains(typeof(OnDemandIngestionWorker), workers);
    }

    [Fact]
    public async Task WorkersResolveFromTheZones()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        await using var services = host.BuildServices(DdragonFixtures.CreateReplay());

        using var job = ActivatorUtilities.CreateInstance<PatchWatchJob>(services);
        using var versions = ActivatorUtilities.CreateInstance<VersionIngestionWorker>(services);
        using var onDemand = ActivatorUtilities.CreateInstance<OnDemandIngestionWorker>(services);

        Assert.Equal("ingestion.patch-watch", job.Name);
        Assert.Equal(TimeSpan.FromMinutes(10), job.Period);
    }

    [Fact]
    public async Task WatchedVersionIsIngestedByTheWorker()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        await using var services = host.BuildServices(DdragonFixtures.CreateReplay());
        using var job = ActivatorUtilities.CreateInstance<PatchWatchJob>(services);
        using var worker = ActivatorUtilities.CreateInstance<VersionIngestionWorker>(services);

        var first = await job.RunIfDueAsync(Token);
        var second = await job.RunIfDueAsync(Token);
        await worker.StartAsync(Token);
        var promoted = await WaitForPromotionAsync(host);
        await worker.StopAsync(Token);

        Assert.Equal(JobRunOutcome.Succeeded, first);
        Assert.Equal(JobRunOutcome.NotDue, second);
        Assert.Equal(["16.19.1"], promoted);
        Assert.Equal(20, host.CountFiles("data/16.19.1"));
    }

    private static async Task<IReadOnlyList<string>> WaitForPromotionAsync(IngestionHost host)
    {
        var elapsed = Stopwatch.StartNew();
        IReadOnlyList<string> promoted;
        while ((promoted = await host.QueryAsync(Promoted)).Count == 0)
        {
            Assert.True(elapsed.Elapsed < Deadline, "No version promoted within a minute.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), Token);
        }

        return promoted;
    }
}
