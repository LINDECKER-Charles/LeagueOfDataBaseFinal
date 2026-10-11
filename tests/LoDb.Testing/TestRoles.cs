using LoDb.Infrastructure.Persistence.Roles;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OptionsOf = Microsoft.Extensions.Options.Options;

namespace LoDb.Testing;

/// <summary>
/// The database roles of the tests, as <c>migrate</c> keeps them: a known password for the
/// API's role, and no connection limit, since one test server holds the databases of
/// parallel tests.
/// </summary>
/// <remarks>
/// <see cref="PostgresContainerFixture"/> puts them in place when the server starts, with the
/// same settings as every migration of <see cref="TestDatabase"/>: those then find the roles
/// as they want them and never write them, whatever other database migrates at the same time.
/// </remarks>
public static class TestRoles
{
    /// <summary>Password of <see cref="DatabaseRoleNames.App"/> on a test server.</summary>
    public const string AppPassword = "lodb-app-test-password";

    /// <summary>The settings of every migration a test runs.</summary>
    public static AppRoleOptions Options => new()
    {
        Password = AppPassword,
        ConnectionLimit = AppRoleOptions.UnlimitedConnections,
    };

    /// <summary>The roles with <see cref="Options"/>.</summary>
    public static DatabaseRoles Create() => Create(Options);

    /// <summary>The roles with other settings, for a test of the roles themselves.</summary>
    public static DatabaseRoles Create(AppRoleOptions options) =>
        new(OptionsOf.Create(options), NullLogger<DatabaseRoles>.Instance);

    /// <summary>
    /// <paramref name="connectionString"/> signed in as the API's role, the way the API
    /// connects on a served host.
    /// </summary>
    public static string AppConnectionString(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Username = DatabaseRoleNames.App,
            Password = AppPassword,
        }.ConnectionString;
}
