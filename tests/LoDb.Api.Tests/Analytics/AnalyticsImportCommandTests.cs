using System.Text.Json;
using LoDb.Api.Cli;
using LoDb.Infrastructure.Analytics;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Analytics;

/// <summary>
/// <c>analytics import</c> over a legacy <c>analytics/daily</c> folder: the day files become
/// <c>import</c> aggregates, anything else is refused, and a second run adds nothing.
/// </summary>
/// <remarks>
/// The equivalence of an imported day with the legacy one is the infrastructure's tests'
/// (<c>LegacyDailyImportTests</c>); these check the command around it.
/// </remarks>
public sealed class AnalyticsImportCommandTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 7, 14);

    private readonly DirectoryInfo _source =
        Directory.CreateTempSubdirectory("lodb-analytics-daily-");

    private TestDatabase? _database;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private TestDatabase Database =>
        _database ?? throw new InvalidOperationException("The database is not created yet.");

    [Fact]
    public void ApiDeclaresTheAnalyticsImportCommand()
    {
        var catalog = CliCommandCatalog.Discover(typeof(Program).Assembly);

        var command = Assert.Single(catalog.Commands, static c => c.Name == "analytics import");
        Assert.True(command.RequiresHost);
    }

    [Fact]
    public async Task DayFilesBecomeImportedAggregates()
    {
        WriteSample();

        var exitCode = await RunAsync("--source", _source.FullName);

        Assert.Equal(CliExitCodes.Success, exitCode);
        var row = Assert.Single(await DailyAsync());
        Assert.Equal((Day, AnalyticsDailySource.Import), (row.Day, row.Source));
        using var totals = JsonDocument.Parse(row.Totals);
        Assert.Equal(3, totals.RootElement.GetProperty("views").GetInt64());
        Assert.Equal(1, totals.RootElement.GetProperty("botViews").GetInt64());
    }

    [Fact]
    public async Task SecondRunAddsNothing()
    {
        WriteSample();
        await RunAsync("--source", _source.FullName);
        var first = Assert.Single(await DailyAsync());

        var exitCode = await RunAsync("--source", _source.FullName);

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(first.UpdatedAt, Assert.Single(await DailyAsync()).UpdatedAt);
    }

    [Fact]
    public async Task DryRunWritesNothing()
    {
        WriteSample();

        var exitCode = await RunAsync("--source", _source.FullName, "--dry-run");

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Empty(await DailyAsync());
    }

    [Fact]
    public async Task MissingDirectoryFails()
    {
        var exitCode = await RunAsync("--source", Path.Combine(_source.FullName, "absent"));

        Assert.Equal(CliExitCodes.Failure, exitCode);
    }

    [Theory]
    [InlineData("--dry-run")]
    [InlineData("--source")]
    [InlineData("--source", "a", "--source", "b")]
    [InlineData("--source", "a", "--days", "3")]
    public async Task InvalidArgumentsAreAUsageError(params string[] arguments)
    {
        Assert.Equal(CliExitCodes.Usage, await RunAsync(arguments));
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

        _source.Delete(recursive: true);
    }

    // Settings first: a bare --dry-run last never takes one for its value.
    private Task<int> RunAsync(params string[] arguments) =>
        CliRunner.RunAsync(
            [
                "analytics", "import",
                $"--ConnectionStrings:LoDb={Database.ConnectionString}",
                "--Logging:LogLevel:Default=Warning",
                .. arguments,
            ],
            typeof(Program).Assembly,
            static (services, configuration) =>
                services.AddLoDbPersistence(configuration).AddLoDbAnalytics(configuration));

    private async Task<List<AnalyticsDaily>> DailyAsync()
    {
        await using var context = Database.CreateContext();
        return await context.AnalyticsDaily.AsNoTracking()
            .OrderBy(static row => row.Day)
            .ToListAsync(Cancellation);
    }

    // One day as the legacy stack writes it, a day file it could not have written, and a
    // file of another kind: only the first is imported.
    private void WriteSample()
    {
        Write(
            "2026-07-14.json",
            """
            {"date":"2026-07-14","views":3,"botViews":1,"visitors":["deadbeefcafe0001"],
             "byType":{"item":2,"home":1},"byKind":{"list":2,"home":1},
             "byRoute":{"app_items":2,"app_home":1},"status":{"200":3},
             "pages":{"/objects":2,"/":1},"entities":{},"countryNames":{}}
            """);
        Write("2026-07-15.json", "{not json");
        Write("notes.json", "{}");
    }

    private void Write(string name, string content) =>
        File.WriteAllText(Path.Combine(_source.FullName, name), content);
}
