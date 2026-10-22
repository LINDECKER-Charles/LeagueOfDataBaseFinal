using System.Net;
using LoDb.Api.Cli;
using LoDb.Api.Tests.Ingestion;
using LoDb.Infrastructure.Locks;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Cli;

/// <summary>
/// <c>catalog states build</c> as the deploy runs it, over the recorded Data Dragon: an index
/// that lags stays served when the list of versions cannot be read, while no index at all, or
/// a lock another writer keeps, stops the deploy.
/// </summary>
public sealed class CatalogStatesBuildCommandTests(PostgresContainerFixture postgres)
{
    private const string Current = "SELECT id FROM catalog_state_generation WHERE is_current";
    private const string IndexLock = "catalog:states";
    private const string VersionList = "/api/versions.json";

    private static CancellationToken Token => IngestionHost.Token;

    [Fact]
    public async Task CurrentIndexIsLeftAsItIsWhenTheListCannotBeRead()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        await PromotedEnglishCatalog.StoreAsync(host);
        var built = await RunAsync(host, DdragonFixtures.CreateReplay());
        var generation = await host.QueryAsync(Current);

        var deferred = await RunAsync(host, Unlisted());
        var ingesting = await RunAsync(host, Unlisted(), "--ingest-missing");

        Assert.Equal(
            (CliExitCodes.Success, CliExitCodes.Success, CliExitCodes.Failure),
            (built, deferred, ingesting));
        Assert.Single(generation);
        Assert.Equal(generation, await host.QueryAsync(Current));
    }

    // The first deploy, or a new fingerprint scheme: the API would start on no index.
    [Fact]
    public async Task NoCurrentIndexAndAnUnreadableListStopTheDeploy()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        await PromotedEnglishCatalog.StoreAsync(host);

        var exitCode = await RunAsync(host, Unlisted());

        Assert.Equal(CliExitCodes.Failure, exitCode);
        Assert.Empty(await host.QueryAsync(Current));
    }

    [Fact]
    public async Task LockKeptByAnotherWriterFailsTheCommand()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        await PromotedEnglishCatalog.StoreAsync(host);
        await using var locks = new PostgresDistributedLock(host.Database.AppConnectionString);
        await using var held = await locks.TryAcquireAsync(IndexLock, Token);
        var replay = DdragonFixtures.CreateReplay();

        var exitCode = await RunAsync(host, (services, configuration) =>
        {
            IngestionHost.AddZones(services, configuration, replay);
            services.AddSingleton<TimeProvider>(new HastyClock());
        });

        Assert.NotNull(held);
        Assert.Equal(CliExitCodes.Failure, exitCode);
        Assert.Empty(await host.QueryAsync(Current));
    }

    // Data Dragon's list of versions out of reach, for good.
    private static FixtureReplayHandler Unlisted()
    {
        var replay = DdragonFixtures.CreateReplay();
        replay.FailWith(
            static url => url.AbsolutePath == VersionList,
            HttpStatusCode.ServiceUnavailable);
        return replay;
    }

    private static Task<int> RunAsync(
        IngestionHost host,
        FixtureReplayHandler replay,
        params string[] switches) =>
        RunAsync(
            host,
            (services, configuration) => IngestionHost.AddZones(services, configuration, replay),
            switches);

    // Settings first: a switch last never takes one for its value.
    private static Task<int> RunAsync(
        IngestionHost host,
        Action<IServiceCollection, IConfiguration> addServices,
        params string[] switches) =>
        CliRunner.RunAsync(
            ["catalog", "states", "build", .. host.Settings, .. switches],
            typeof(Program).Assembly,
            addServices);

    // Each reading ten minutes after the last one: the minute the command waits for the lock
    // is spent at the first refusal.
    private sealed class HastyClock : TimeProvider
    {
        private const long StepSeconds = 600;

        private long timestamp;

        public override long GetTimestamp() =>
            Interlocked.Add(ref timestamp, TimestampFrequency * StepSeconds);
    }
}
