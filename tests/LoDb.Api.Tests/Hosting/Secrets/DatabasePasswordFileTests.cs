using System.Net;
using System.Reflection;
using LoDb.Api.Cli;
using LoDb.Api.Hosting.Secrets;
using LoDb.Api.Tests.Cli;
using LoDb.Testing;
using Npgsql;

namespace LoDb.Api.Tests.Hosting.Secrets;

/// <summary>
/// A served host passes the connection string without its password and the password as a
/// secret file: the API and <c>migrate</c> reach the database with it.
/// </summary>
/// <remarks>
/// In the group of the entry point's runs: <c>migrate</c> goes through Program.cs, whose
/// environment any concurrent run could change.
/// </remarks>
[Collection(DevelopmentHostGroup.Name)]
public sealed class DatabasePasswordFileTests(PostgresContainerFixture postgres)
    : IDisposable
{
    private readonly SecretFolder _folder = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.ServiceUnavailable)]
    public async Task ApiReachesTheDatabaseWithThePasswordOfTheFile(
        bool hasPasswordFile,
        HttpStatusCode readiness)
    {
        if (hasPasswordFile)
        {
            WritePasswordFile(postgres.ConnectionString);
        }

        await using var factory = new ApiFactory
        {
            PostgresConnectionString = WithoutPassword(postgres.ConnectionString),
            Settings = new Dictionary<string, string?>
            {
                [SecretFileSet.FolderKey] = _folder.Path,
            },
        };
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/readyz", UriKind.Relative),
            Cancellation);

        Assert.Equal(readiness, response.StatusCode);
    }

    [Fact]
    public async Task MigrateReachesTheDatabaseWithThePasswordOfTheFile()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        string[] args =
        [
            "migrate",
            $"--ConnectionStrings:LoDb={WithoutPassword(database.ConnectionString)}",
            $"--{SecretFileSet.FolderKey}={_folder.Path}",
            "--Logging:LogLevel:Default=Warning",
        ];

        WritePasswordFile(database.ConnectionString);

        var exitCode = await RunEntryPointAsync(args);

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.NotEmpty(await database.QueryAsync(
            "SELECT migration_id FROM \"__EFMigrationsHistory\"",
            Cancellation));
    }

    public void Dispose() => _folder.Dispose();

    private static string WithoutPassword(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Password = null }.ConnectionString;

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

    private void WritePasswordFile(string connectionString)
    {
        var password = new NpgsqlConnectionStringBuilder(connectionString).Password;
        Assert.False(string.IsNullOrEmpty(password), "The test server must ask for a password.");
        _folder.With(DatabasePassword.Key, password);
    }
}
