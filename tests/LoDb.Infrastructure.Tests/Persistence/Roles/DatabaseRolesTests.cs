using LoDb.Infrastructure.Persistence.Roles;
using LoDb.Testing;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.Roles;

/// <summary>
/// The roles <c>migrate</c> keeps on the server: created once with the decided settings and
/// no attribute beyond their grants, then written again only when the configuration or a
/// hand-made change differs. On a server of their own: these tests change its roles.
/// </summary>
public sealed class DatabaseRolesTests(IsolatedPostgres isolated) : IClassFixture<IsolatedPostgres>
{
    private const string Password = "first-app-password";
    private const string NextPassword = "next-app-password";
    private const string InvalidPassword = "28P01";

    private const string RolesSql = """
        SELECT format('%s login=%s super=%s createdb=%s createrole=%s replication=%s'
                || ' bypassrls=%s limit=%s',
            rolname, rolcanlogin, rolsuper, rolcreatedb, rolcreaterole, rolreplication,
            rolbypassrls, rolconnlimit)
        FROM pg_roles
        WHERE rolname IN ('lodb_owner', 'lodb_partitions', 'lodb_app')
        ORDER BY rolname
        """;

    private const string AppSettingsSql = """
        SELECT unnest(s.setconfig)
        FROM pg_db_role_setting s
        WHERE s.setrole = 'lodb_app'::regrole AND s.setdatabase = 0
        """;

    private const string AppRowVersionSql =
        "SELECT xmin::text FROM pg_authid WHERE rolname = 'lodb_app'";

    private const string AppMembershipsSql =
        "SELECT roleid::regrole::text FROM pg_auth_members WHERE member = 'lodb_app'::regrole";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private PostgresContainerFixture Server => isolated.Server;

    [Fact]
    public async Task FirstRunCreatesTheRolesAsDecided()
    {
        await using var database = await Server.CreateDatabaseAsync(Cancellation);
        await DropRolesAsync(database);

        var changes = await ApplyAsync(database, new AppRoleOptions { Password = Password });

        Assert.Equal(new RoleChanges(3, 0, PasswordSet: true), changes);
        Assert.Equal(
            [
                "lodb_app login=t super=f createdb=f createrole=f replication=f"
                    + " bypassrls=f limit=60",
                "lodb_owner login=f super=f createdb=f createrole=f replication=f"
                    + " bypassrls=f limit=-1",
                "lodb_partitions login=f super=f createdb=f createrole=f replication=f"
                    + " bypassrls=f limit=-1",
            ],
            await database.QueryAsync(RolesSql, Cancellation));
        Assert.Equal(
            ["statement_timeout=15000ms"],
            await database.QueryAsync(AppSettingsSql, Cancellation));
        Assert.Equal("15s", await ShowAsAppAsync(database, Password, "statement_timeout"));
    }

    [Fact]
    public async Task SecondRunWritesNoRole()
    {
        await using var database = await Server.CreateDatabaseAsync(Cancellation);
        var options = new AppRoleOptions { Password = Password };
        await ApplyAsync(database, options);
        var version = await database.QueryAsync(AppRowVersionSql, Cancellation);

        var changes = await ApplyAsync(database, options);

        Assert.True(changes.IsEmpty);
        Assert.Equal(version, await database.QueryAsync(AppRowVersionSql, Cancellation));
    }

    [Fact]
    public async Task NewSettingsAndPasswordReplaceTheCurrentOnes()
    {
        await using var database = await Server.CreateDatabaseAsync(Cancellation);
        await ApplyAsync(database, new AppRoleOptions { Password = Password });

        var changes = await ApplyAsync(database, new AppRoleOptions
        {
            Password = NextPassword,
            StatementTimeout = TimeSpan.FromSeconds(30),
            ConnectionLimit = 10,
        });

        Assert.Equal(new RoleChanges(0, 1, PasswordSet: true), changes);
        Assert.Equal("30s", await ShowAsAppAsync(database, NextPassword, "statement_timeout"));
        var refused = await Assert.ThrowsAsync<PostgresException>(
            () => ShowAsAppAsync(database, Password, "statement_timeout"));
        Assert.Equal(InvalidPassword, refused.SqlState);
    }

    [Fact]
    public async Task UnsetPasswordKeepsTheCurrentOne()
    {
        await using var database = await Server.CreateDatabaseAsync(Cancellation);
        await ApplyAsync(database, new AppRoleOptions { Password = Password });

        var changes = await ApplyAsync(database, new AppRoleOptions());

        Assert.False(changes.PasswordSet);
        Assert.Equal("15s", await ShowAsAppAsync(database, Password, "statement_timeout"));
    }

    [Fact]
    public async Task PasswordPostgresHashedItselfIsRecognised()
    {
        await using var database = await Server.CreateDatabaseAsync(Cancellation);
        await ApplyAsync(database, new AppRoleOptions { Password = Password });
        await database.ExecuteAsync($"ALTER ROLE lodb_app PASSWORD '{NextPassword}'", Cancellation);

        var changes = await ApplyAsync(database, new AppRoleOptions { Password = NextPassword });

        Assert.False(changes.PasswordSet);
    }

    [Fact]
    public async Task AttributeGrantedByHandIsTakenBack()
    {
        await using var database = await Server.CreateDatabaseAsync(Cancellation);
        await ApplyAsync(database, new AppRoleOptions { Password = Password });
        await database.ExecuteAsync("ALTER ROLE lodb_app CREATEDB CREATEROLE", Cancellation);
        await database.ExecuteAsync("ALTER ROLE lodb_owner LOGIN", Cancellation);

        var changes = await ApplyAsync(database, new AppRoleOptions { Password = Password });

        Assert.Equal(new RoleChanges(0, 2, PasswordSet: false), changes);
        Assert.All(
            await database.QueryAsync(RolesSql, Cancellation),
            static role =>
                Assert.Contains("createdb=f createrole=f", role, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MembershipGrantedByHandIsTakenBack()
    {
        await using var database = await Server.CreateDatabaseAsync(Cancellation);
        var options = new AppRoleOptions { Password = Password };
        await ApplyAsync(database, options);
        await database.ExecuteAsync(
            "GRANT lodb_owner, pg_read_server_files TO lodb_app",
            Cancellation);

        await ApplyAsync(database, options);

        Assert.Empty(await database.QueryAsync(AppMembershipsSql, Cancellation));
    }

    private static async Task<RoleChanges> ApplyAsync(
        TestDatabase database,
        AppRoleOptions options)
    {
        await using var connection = await database.DataSource.OpenConnectionAsync(Cancellation);
        return await TestRoles.Create(options).ApplyAsync(connection, Cancellation);
    }

    // The fixture put the tests' roles in place; the databases of the previous tests, which
    // held their grants, are dropped.
    private static Task DropRolesAsync(TestDatabase database) =>
        database.ExecuteAsync(
            "DROP ROLE IF EXISTS lodb_app, lodb_partitions, lodb_owner",
            Cancellation);

    private static async Task<string> ShowAsAppAsync(
        TestDatabase database,
        string password,
        string setting)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Username = DatabaseRoleNames.App,
            Password = password,
            Pooling = false,
        }.ConnectionString;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand($"SHOW {setting}", connection);
        return (string)(await command.ExecuteScalarAsync(Cancellation))!;
    }
}
