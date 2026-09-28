using System.Text.Json.Nodes;
using LoDb.Api.Cli;
using LoDb.Testing;
using LoDb.Testing.Fixtures;

namespace LoDb.Api.Tests.Ingestion;

/// <summary>
/// <c>catalog export</c> as the parity check runs it, over the recorded Data Dragon: the
/// catalog ingested if need be and written whole, or an exit code telling why not.
/// </summary>
public sealed class CatalogExportCommandTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>
{
    private static CancellationToken Token => IngestionHost.Token;

    [Fact]
    public void ApiDeclaresTheCatalogExportCommand()
    {
        var catalog = CliCommandCatalog.Discover(typeof(Program).Assembly);

        var invocation = catalog.Match(["catalog", "export", "--version", "16.19.1"]);

        Assert.NotNull(invocation);
        Assert.Equal("catalog export", invocation.Command.Name);
        Assert.True(invocation.Command.RequiresHost);
        Assert.Equal(["--version", "16.19.1"], invocation.Arguments);
    }

    [Fact]
    public async Task ColdCatalogIsIngestedThenWritten()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        var output = Path.Combine(host.StorageRoot, "export.json");

        var exitCode = await RunAsync(
            host,
            DdragonFixtures.CreateReplay(),
            "--version", "16.19.1", "--lang", "fr_FR", "--output", output);

        Assert.Equal(CliExitCodes.Success, exitCode);
        var projection = JsonNode.Parse(await File.ReadAllTextAsync(output, Token))!;
        var champions = projection["champions"]!["entries"]!.AsArray();
        Assert.Equal("fr_FR", (string?)projection["language"]);
        Assert.Equal(5, champions.Count);
        Assert.All(
            champions,
            static champion => Assert.Equal("present", (string?)champion!["image"]!["status"]));
        Assert.Equal(4, host.CountFiles("data/16.19.1/en_US"));
        Assert.Equal(4, host.CountFiles("data/16.19.1/fr_FR"));
        Assert.False(File.Exists(output + ".partial"));
    }

    [Fact]
    public async Task StoredCatalogIsExportedAgainIdentically()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        var first = Path.Combine(host.StorageRoot, "first.json");
        var second = Path.Combine(host.StorageRoot, "second.json");
        await RunAsync(host, DdragonFixtures.CreateReplay(), Export(first));
        var replay = DdragonFixtures.CreateReplay();

        var exitCode = await RunAsync(host, replay, [.. Export(second), "--stored-only"]);

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(
            await File.ReadAllBytesAsync(first, Token),
            await File.ReadAllBytesAsync(second, Token));
        Assert.Empty(replay.Requests);
    }

    [Fact]
    public async Task StoredOnlyExportOfAnEmptyStoreFails()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        var output = Path.Combine(host.StorageRoot, "export.json");
        var replay = DdragonFixtures.CreateReplay();

        var exitCode = await RunAsync(host, replay, [.. Export(output), "--stored-only"]);

        Assert.Equal(CliExitCodes.Failure, exitCode);
        Assert.False(File.Exists(output));
        Assert.Empty(replay.Requests);
    }

    [Fact]
    public async Task UnknownCatalogFails()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        var output = Path.Combine(host.StorageRoot, "export.json");

        var exitCode = await RunAsync(
            host,
            DdragonFixtures.CreateReplay(),
            "--version", "99.1.1", "--lang", "en_US", "--output", output);

        Assert.Equal(CliExitCodes.Failure, exitCode);
        Assert.False(File.Exists(output));
        Assert.Equal(0, host.CountFiles("data"));
    }

    [Fact]
    public async Task FailedWriteLeavesNothingBehind()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        // A directory: the document is written aside, then cannot replace it.
        var output = Directory.CreateDirectory(Path.Combine(host.StorageRoot, "taken")).FullName;

        var exitCode = await RunAsync(host, DdragonFixtures.CreateReplay(), Export(output));

        Assert.Equal(CliExitCodes.Failure, exitCode);
        Assert.False(File.Exists(output + ".partial"));
        Assert.True(Directory.Exists(output));
    }

    [Fact]
    public async Task InvalidArgumentsAreAUsageError()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        var replay = DdragonFixtures.CreateReplay();

        var exitCode = await RunAsync(host, replay, "--version", "16.19.1");

        Assert.Equal(CliExitCodes.Usage, exitCode);
        Assert.Empty(replay.Requests);
    }

    private static string[] Export(string output) =>
        ["--version", "16.19.1", "--lang", "en_US", "--output", output];

    // Settings first: a bare --stored-only last never takes one for its value.
    private static Task<int> RunAsync(
        IngestionHost host,
        FixtureReplayHandler replay,
        params string[] arguments) =>
        CliRunner.RunAsync(
            ["catalog", "export", .. host.Settings, .. arguments],
            typeof(Program).Assembly,
            (services, configuration) => IngestionHost.AddZones(services, configuration, replay));
}
