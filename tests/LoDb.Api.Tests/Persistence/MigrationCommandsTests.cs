using LoDb.Api.Cli;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Roles;
using LoDb.Testing;

namespace LoDb.Api.Tests.Persistence;

/// <summary>
/// <c>migrate</c> and <c>baseline mark-applied</c> as the Compose service and an operator
/// run them: the exit code tells whether the database may serve the API.
/// </summary>
public sealed class MigrationCommandsTests(PostgresContainerFixture postgres)
{
    private const string Baseline = "20260719150001_Baseline";
    private const string Lot1 = "20260926022150_Lot1DataDragon";
    private const string Lot4 = "20260926091745_Lot4Accounts";
    private const string Lot6 = "20260926185409_Lot6BillingAnalyticsApps";
    private const string PaymentStates = "20261010231234_StripePaymentStates";
    private const string QueryStatistics = "20261010232259_QueryStatistics";
    private const string IdentifierScrub = "20261011073626_AuditIdentifierScrub";
    private const string RefreshFamilies = "20261011075322_RefreshTokenFamilies";
    private const string AudienceEntityDaily = "20261011092929_AudienceEntityDaily";
    private const string BackupRuns = "20261012081618_BackupRuns";
    private const string PrerenderPasses = "20261020124135_PrerenderPasses";
    private const string PrerenderBuildSizes = "20261020142031_PrerenderBuildSizes";
    private const string PrerenderDiagnostics = "20261020235519_PrerenderDiagnostics";
    private const string PrerenderDemand = "20261021213406_PrerenderDemand";
    private const string PrerenderPaceSetting = "20261021213445_PrerenderPaceSetting";
    private const string CatalogStateIndex = "20261022103012_CatalogStateIndex";
    private const string EfHistory = "__EFMigrationsHistory";

    private static readonly string UnlimitedAppRoleConnections =
        $"--LoDb:Database:AppRole:ConnectionLimit={AppRoleOptions.UnlimitedConnections}";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

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
            [
                Baseline, Lot1, Lot4, Lot6, PaymentStates, QueryStatistics, IdentifierScrub,
                RefreshFamilies, AudienceEntityDaily, BackupRuns, PrerenderPasses,
                PrerenderBuildSizes, PrerenderDiagnostics, PrerenderDemand,
                PrerenderPaceSetting, CatalogStateIndex,
            ],
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

    // As the container runs it: settings after the command words, no settings file. The
    // API's role keeps no connection limit: the test server holds parallel tests' sessions.
    private static Task<int> RunAsync(string command, string? connectionString)
    {
        string[] connection = connectionString is null
            ? []
            : [$"--ConnectionStrings:LoDb={connectionString}"];
        return CliRunner.RunAsync(
            [
                .. command.Split(' '),
                "--Logging:LogLevel:Default=Warning",
                UnlimitedAppRoleConnections,
                .. connection,
            ],
            typeof(Program).Assembly,
            static (services, configuration) => services.AddLoDbPersistence(configuration));
    }
}
