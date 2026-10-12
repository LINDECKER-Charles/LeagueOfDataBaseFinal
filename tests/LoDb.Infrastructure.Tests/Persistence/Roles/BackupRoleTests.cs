using LoDb.Infrastructure.Persistence.Roles;
using LoDb.Testing;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.Roles;

/// <summary>
/// The backups' role on the server: signed in with the password <c>migrate</c> is given, kept
/// when none is, and a member of <c>pg_read_all_data</c> alone. On a server of its own: these
/// tests change its roles.
/// </summary>
public sealed class BackupRoleTests(IsolatedPostgres isolated) : IClassFixture<IsolatedPostgres>
{
    private const string Password = "first-backup-password";
    private const string NextPassword = "next-backup-password";
    private const string InvalidPassword = "28P01";

    private const string MembershipsSql = """
        SELECT roleid::regrole::text
        FROM pg_auth_members
        WHERE member = 'lodb_sauvegarde'::regrole
        ORDER BY 1
        """;

    private static readonly AppRoleOptions App = new() { Password = TestRoles.AppPassword };

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private PostgresContainerFixture Server => isolated.Server;

    [Fact]
    public async Task PasswordIsSetThenReplaced()
    {
        await using var database = await Server.CreateDatabaseAsync(Cancellation);
        await ApplyAsync(database, Password);

        var changes = await ApplyAsync(database, NextPassword);

        Assert.True(changes.PasswordSet);
        Assert.Equal(1, await SelectOneAsBackupAsync(database, NextPassword));
        var refused = await Assert.ThrowsAsync<PostgresException>(
            () => SelectOneAsBackupAsync(database, Password));
        Assert.Equal(InvalidPassword, refused.SqlState);
    }

    [Fact]
    public async Task UnsetPasswordKeepsTheCurrentOne()
    {
        await using var database = await Server.CreateDatabaseAsync(Cancellation);
        await ApplyAsync(database, Password);

        var changes = await ApplyAsync(database, password: null);

        Assert.False(changes.PasswordSet);
        Assert.Equal(1, await SelectOneAsBackupAsync(database, Password));
    }

    [Fact]
    public async Task MembershipsGrantedByHandAreTakenBackButTheReadingOne()
    {
        await using var database = await Server.CreateDatabaseAsync(Cancellation);
        await ApplyAsync(database, Password);
        await database.ExecuteAsync(
            "GRANT lodb_owner, pg_write_all_data TO lodb_sauvegarde",
            Cancellation);

        await ApplyAsync(database, Password);

        Assert.Equal(["pg_read_all_data"], await database.QueryAsync(MembershipsSql, Cancellation));
    }

    private static async Task<RoleChanges> ApplyAsync(TestDatabase database, string? password)
    {
        await using var connection = await database.DataSource.OpenConnectionAsync(Cancellation);
        var backup = new BackupRoleOptions { Password = password };
        return await TestRoles.Create(App, backup).ApplyAsync(connection, Cancellation);
    }

    private static async Task<int> SelectOneAsBackupAsync(TestDatabase database, string password)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Username = DatabaseRoleNames.Backup,
            Password = password,
            Pooling = false,
        }.ConnectionString;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        return (int)(await command.ExecuteScalarAsync(Cancellation))!;
    }
}
