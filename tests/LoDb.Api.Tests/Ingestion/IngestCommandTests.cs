using LoDb.Api.Cli;
using LoDb.Testing;
using LoDb.Testing.Fixtures;

namespace LoDb.Api.Tests.Ingestion;

/// <summary>
/// <c>ingest</c> as an operator runs it, over the recorded Data Dragon: the exit code tells
/// whether every version completed, and a partial run leaves the version's state alone.
/// </summary>
public sealed class IngestCommandTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>
{
    private const string CountAssets = "SELECT count(*) FROM ddragon_asset";
    private const string CountVersions = "SELECT count(*) FROM ddragon_version";
    private const string Promoted =
        "SELECT version FROM ddragon_version WHERE status = 'ready' AND promoted_at IS NOT NULL";

    [Fact]
    public void ApiDeclaresTheIngestCommand()
    {
        var catalog = CliCommandCatalog.Discover(typeof(Program).Assembly);

        var command = Assert.Single(
            catalog.Commands,
            static command => command.Name == "ingest");
        Assert.True(command.RequiresHost);
    }

    [Fact]
    public async Task NamedVersionIsIngestedInTheLanguagesAsked()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        var replay = DdragonFixtures.CreateReplay();

        var exitCode = await RunAsync(host, replay, "--version", "16.19.1", "--languages", "en_US");

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(4, host.CountFiles("data/16.19.1/en_US"));
        Assert.Equal(0, host.CountFiles("data/16.19.1/fr_FR"));
        Assert.Equal(["81"], await host.QueryAsync(CountAssets));
        Assert.Equal(["0"], await host.QueryAsync(CountVersions));
        Assert.DoesNotContain(
            replay.Requests,
            static url => url.AbsolutePath.Contains("/fr_FR/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NewestVersionsAreIngestedWholeAndPromoted()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);

        var exitCode = await RunAsync(host, DdragonFixtures.CreateReplay(), "--latest", "1");

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(20, host.CountFiles("data/16.19.1"));
        Assert.Equal(["16.19.1"], await host.QueryAsync(Promoted));
    }

    [Fact]
    public async Task ForcedRunFetchesTheImagesAgain()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        await RunAsync(host, DdragonFixtures.CreateReplay(), "--latest", "1");
        var replay = DdragonFixtures.CreateReplay();

        var exitCode = await RunAsync(host, replay, "--version", "16.19.1", "--force");

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(
            81,
            replay.Requests.Count(static url =>
                url.AbsolutePath.Contains("/img/", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            replay.Requests,
            static url => url.AbsolutePath.Contains("/data/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnknownVersionFails()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);

        var exitCode = await RunAsync(host, DdragonFixtures.CreateReplay(), "--version", "99.1.1");

        Assert.Equal(CliExitCodes.Failure, exitCode);
        Assert.Equal(0, host.CountFiles("data"));
        Assert.Equal(["0"], await host.QueryAsync(CountVersions));
    }

    [Fact]
    public async Task InvalidArgumentsAreAUsageError()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        var replay = DdragonFixtures.CreateReplay();

        var exitCode = await RunAsync(host, replay, "--latest", "0");

        Assert.Equal(CliExitCodes.Usage, exitCode);
        Assert.Empty(replay.Requests);
    }

    // Settings first: a bare --force last never takes one for its value.
    private static Task<int> RunAsync(
        IngestionHost host,
        FixtureReplayHandler replay,
        params string[] arguments) =>
        CliRunner.RunAsync(
            ["ingest", .. host.Settings, .. arguments],
            typeof(Program).Assembly,
            (services, configuration) => IngestionHost.AddZones(services, configuration, replay));
}
