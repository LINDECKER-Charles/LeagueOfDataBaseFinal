using System.Reflection;
using LoDb.Api.Cli;
using LoDb.Infrastructure.Persistence.Roles;
using LoDb.Testing;

namespace LoDb.Api.Tests.Cli;

/// <summary>
/// Every command that needs a host, run through the API's entry point in
/// <c>Development</c>, as the dev stack runs them: the command host holds every service
/// Program.cs registers and validates them all when it is built, so a registration that
/// only the web host can satisfy fails every command.
/// </summary>
/// <remarks>
/// <see cref="HostedCommandsTests"/> fails when a command with a host is missing here.
/// </remarks>
[Collection(DevelopmentHostGroup.Name)]
public sealed class DevelopmentCommandHostTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string EnvironmentVariable = "ASPNETCORE_ENVIRONMENT";
    private const string Development = "Development";
    private const string AdminEmail = "dev.admin@example.test";

    private static readonly string[] SuperuserCommands = ["migrate", "baseline mark-applied"];

    private readonly DirectoryInfo _storage =
        Directory.CreateTempSubdirectory("lodb-cli-development-storage-");

    private readonly DirectoryInfo _source =
        Directory.CreateTempSubdirectory("lodb-cli-development-source-");

    private TestDatabase? _database;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private TestDatabase Database =>
        _database ?? throw new InvalidOperationException("The database is not created yet.");

    [Fact]
    public async Task AdminCreateRuns()
    {
        var exitCode = await RunAsync(["admin", "create"], "--email", AdminEmail);

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(
            ["1"],
            await Database.QueryAsync(
                $"SELECT count(*)::text FROM users WHERE email = '{AdminEmail}'",
                Cancellation));
    }

    [Fact]
    public async Task AdminMfaCommandsRun()
    {
        await RunAsync(["admin", "create"], "--email", AdminEmail);

        var reset = await RunAsync(["admin", "mfa-reset"], "--email", AdminEmail);
        var invite = await RunAsync(["admin", "mfa-invite"], "--email", AdminEmail);

        Assert.Equal((CliExitCodes.Success, CliExitCodes.Success), (reset, invite));
    }

    [Fact]
    public async Task AnalyticsImportDryRunRuns()
    {
        var exitCode = await RunAsync(
            ["analytics", "import"],
            "--source",
            _source.FullName,
            "--dry-run");

        Assert.Equal(CliExitCodes.Success, exitCode);
    }

    // No host may be reached: the version fails upstream, after the host was built.
    [Fact]
    public async Task IngestRuns()
    {
        var exitCode = await RunAsync(["ingest"], "--version", "16.19.1", "--languages", "en_US");

        Assert.Equal(CliExitCodes.Failure, exitCode);
    }

    [Theory]
    [InlineData("migrate")]
    [InlineData("baseline mark-applied")]
    public async Task CommandOfHostSettingsOnlyRuns(string command)
    {
        Assert.Equal(CliExitCodes.Success, await RunAsync(command.Split(' ')));
    }

    // An unknown option stops these before any work, once their host is built. The MFA
    // commands' hosts are built by AdminMfaCommandsRun.
    [Theory]
    [InlineData("admin root")]
    [InlineData("audit import")]
    [InlineData("catalog export")]
    [InlineData("client-policy publish")]
    [InlineData("prerender pause")]
    [InlineData("prerender resume")]
    [InlineData("prerender unit")]
    public async Task CommandWithOptionsBuildsItsHost(string command)
    {
        Assert.Equal(CliExitCodes.Usage, await RunAsync(command.Split(' '), "--unknown"));
    }

    public async ValueTask InitializeAsync()
    {
        _database = await postgres.CreateMigratedDatabaseAsync(Cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }

        _storage.Delete(recursive: true);
        _source.Delete(recursive: true);
    }

    // Settings first: a bare switch last never takes one for its value. The migrations run as
    // the server's superuser, as their Compose service does; every other command runs in the
    // API's container, as the API's role.
    private async Task<int> RunAsync(string[] command, params string[] arguments)
    {
        var connection = SuperuserCommands.Contains(string.Join(' ', command))
            ? Database.ConnectionString
            : Database.AppConnectionString;
        string[] args =
        [
            .. command,
            $"--ConnectionStrings:LoDb={connection}",
            $"--LoDb:Storage:Root={_storage.FullName}",
            "--LoDb:Egress:AllowedHosts:0=egress.invalid",
            "--LoDb:Egress:RetryBaseDelay=00:00:00.001",
            "--Logging:LogLevel:Default=Warning",
            $"--LoDb:Database:AppRole:ConnectionLimit={AppRoleOptions.UnlimitedConnections}",
            .. arguments,
        ];
        var previous = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Environment.SetEnvironmentVariable(EnvironmentVariable, Development);
        try
        {
            return await RunEntryPointAsync(args);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariable, previous);
        }
    }

    // Program.cs itself, so that the host gets exactly the services the API registers.
    private static async Task<int> RunEntryPointAsync(string[] args)
    {
        var entryPoint = typeof(Program).Assembly.EntryPoint
            ?? throw new InvalidOperationException("The API has no entry point.");
        var result = await Task.Run(
            () => entryPoint.Invoke(null, BindingFlags.DoNotWrapExceptions, null, [args], null),
            Cancellation);
        return result switch
        {
            Task<int> run => await run,
            int exitCode => exitCode,
            _ => throw new InvalidOperationException("The entry point returned no exit code."),
        };
    }
}
