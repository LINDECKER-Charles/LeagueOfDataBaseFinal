using LoDb.Api.Cli;
using LoDb.Api.Modules.ClientPolicy;
using LoDb.Api.Tests.ClientPolicy.Units;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Apps;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.ClientPolicy;

/// <summary>
/// <c>client-policy publish</c>, as a release workflow runs it: the row of the app written
/// whole, and an invalid policy refused before anything is written.
/// </summary>
public sealed class ClientPolicyPublishCommandTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private TestDatabase? _database;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private TestDatabase Database =>
        _database ?? throw new InvalidOperationException("The database is not created yet.");

    [Fact]
    public void ApiDeclaresThePublishCommand()
    {
        var catalog = CliCommandCatalog.Discover(typeof(Program).Assembly);

        var command = Assert.Single(
            catalog.Commands,
            static c => c.Name == "client-policy publish");
        Assert.True(command.RequiresHost);
    }

    [Fact]
    public async Task CommandPublishesThePolicyOfOneApp()
    {
        var exitCode = await RunAsync(
            "--platform", "android",
            "--minimum", "1.2.0",
            "--latest", "1.4.2",
            "--bundle-id", "web-1.4.2",
            "--bundle-url", "https://example.test/shell.zip",
            "--bundle-checksum", PublishPolicyRulesTests.Checksum,
            "--bundle-signature", "c2ln",
            "--bundle-minimum-native", "1.3.0");

        Assert.Equal(CliExitCodes.Success, exitCode);
        var row = Assert.Single(await RowsAsync());
        Assert.Equal(
            (AppPlatform.Android, "1.2.0", "1.4.2", "web-1.4.2", "1.3.0"),
            (row.Platform, row.MinimumVersion, row.LatestVersion, row.BundleId,
                row.BundleMinimumNativeVersion));
    }

    [Fact]
    public async Task RepublishingReplacesTheRow()
    {
        await RunAsync("--platform", "desktop", "--minimum", "1.0.0", "--latest", "1.1.0");

        var exitCode = await RunAsync("--platform", "desktop", "--latest", "1.2.0");

        Assert.Equal(CliExitCodes.Success, exitCode);
        var row = Assert.Single(await RowsAsync());
        Assert.Equal(((string?)null, "1.2.0"), (row.MinimumVersion, row.LatestVersion));
    }

    // Arguments separated by one space.
    [Theory]
    [InlineData("--platform desktop --minimum 2.0.0 --latest 1.0.0")]
    [InlineData("--platform desktop --bundle-id web-1")]
    [InlineData("--platform android --bundle-id web-1")]
    [InlineData("--platform windows")]
    public async Task InvalidPolicyWritesNothing(string line)
    {
        var exitCode = await RunAsync(line.Split(' '));

        Assert.Equal(CliExitCodes.Usage, exitCode);
        Assert.Empty(await RowsAsync());
    }

    public async ValueTask InitializeAsync()
    {
        _database = await postgres.CreateDatabaseAsync(Cancellation);
        await _database.MigrateAsync(Cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    private Task<int> RunAsync(params string[] arguments) =>
        CliRunner.RunAsync(
            [
                "client-policy", "publish",
                $"--ConnectionStrings:LoDb={Database.ConnectionString}",
                "--Logging:LogLevel:Default=Warning",
                .. arguments,
            ],
            typeof(Program).Assembly,
            static (services, configuration) =>
                services.AddLoDbPersistence(configuration).AddClientPolicy(configuration));

    private async Task<List<ClientPolicyEntry>> RowsAsync()
    {
        await using var context = Database.CreateContext();
        return await context.ClientPolicies.AsNoTracking().ToListAsync(Cancellation);
    }
}
