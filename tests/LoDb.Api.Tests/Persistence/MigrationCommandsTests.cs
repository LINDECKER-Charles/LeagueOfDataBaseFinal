using LoDb.Api.Cli;
using LoDb.Infrastructure.Persistence;
using LoDb.Testing;

namespace LoDb.Api.Tests.Persistence;

/// <summary>
/// <c>migrate</c> and <c>baseline mark-applied</c> as the Compose service and an operator
/// run them: the exit code tells whether the database may serve the API.
/// </summary>
public sealed class MigrationCommandsTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>
{
    private const string Baseline = "20260719150001_Baseline";
    private const string Lot1 = "20260926022150_Lot1DataDragon";
    private const string Lot4 = "20260926091745_Lot4Accounts";
    private const string Lot6 = "20260926185409_Lot6BillingAnalyticsApps";
    private const string EfHistory = "__EFMigrationsHistory";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void ApiDeclaresTheMigrationCommands()
    {
        var catalog = CliCommandCatalog.Discover(typeof(Program).Assembly);

        var commands = catalog.Commands
            .Where(static command => command.Name is "migrate" or "baseline mark-applied")
            .ToList();
        Assert.Equal(2, commands.Count);
        Assert.All(commands, static command => Assert.True(command.RequiresHost));
    }

    [Fact]
    public async Task MigrateMarksADoctrineDatabaseThenMigratesItOnce()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.CreateDoctrineSchemaAsync(Cancellation);

        var first = await RunAsync("migrate", database.ConnectionString);
        var second = await RunAsync("migrate", database.ConnectionString);

        Assert.Equal(CliExitCodes.Success, first);
        Assert.Equal(CliExitCodes.Success, second);
        Assert.Equal(
            [Baseline, Lot1, Lot4, Lot6],
            await database.AppliedMigrationsAsync(Cancellation));
    }

    [Fact]
    public async Task MigrateCreatesAnEmptyDatabase()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);

        var exitCode = await RunAsync("migrate", database.ConnectionString);

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(
            [Baseline, Lot1, Lot4, Lot6],
            await database.AppliedMigrationsAsync(Cancellation));
    }

    [Fact]
    public async Task MigrateRefusesAnUnexpectedSchema()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.CreateDoctrineSchemaAsync(Cancellation);
        await database.ExecuteAsync("ALTER TABLE users ADD COLUMN nickname text", Cancellation);

        var exitCode = await RunAsync("migrate", database.ConnectionString);

        Assert.Equal(CliExitCodes.Failure, exitCode);
        Assert.DoesNotContain(EfHistory, await database.TablesAsync(Cancellation));
    }

    [Fact]
    public async Task MarkAppliedRecordsTheBaselineOnly()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.CreateDoctrineSchemaAsync(Cancellation);

        var first = await RunAsync("baseline mark-applied", database.ConnectionString);
        var second = await RunAsync("baseline mark-applied", database.ConnectionString);

        Assert.Equal(CliExitCodes.Success, first);
        Assert.Equal(CliExitCodes.Success, second);
        Assert.Equal(
            [Baseline],
            await database.AppliedMigrationsAsync(Cancellation));
        Assert.DoesNotContain("ddragon_asset", await database.TablesAsync(Cancellation));
    }

    [Fact]
    public async Task MarkAppliedRefusesAnEmptyDatabase()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);

        var exitCode = await RunAsync("baseline mark-applied", database.ConnectionString);

        Assert.Equal(CliExitCodes.Failure, exitCode);
        Assert.Empty(await database.TablesAsync(Cancellation));
    }

    [Theory]
    [InlineData("migrate")]
    [InlineData("baseline mark-applied")]
    public async Task MissingConnectionStringFailsAsACommandError(string command)
    {
        var exitCode = await RunAsync(command, connectionString: null);

        Assert.Equal(CliExitCodes.Failure, exitCode);
    }

    // As the container runs it: settings after the command words, no settings file.
    private static Task<int> RunAsync(string command, string? connectionString)
    {
        string[] connection = connectionString is null
            ? []
            : [$"--ConnectionStrings:LoDb={connectionString}"];
        return CliRunner.RunAsync(
            [.. command.Split(' '), "--Logging:LogLevel:Default=Warning", .. connection],
            typeof(Program).Assembly,
            static (services, configuration) => services.AddLoDbPersistence(configuration));
    }
}
