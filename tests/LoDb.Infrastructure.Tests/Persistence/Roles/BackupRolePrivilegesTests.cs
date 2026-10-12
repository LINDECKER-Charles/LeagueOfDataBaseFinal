using LoDb.Infrastructure.Persistence.Roles;
using LoDb.Testing;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.Roles;

/// <summary>
/// What the backups' role may do on a migrated database: read every row, dump the whole
/// database, append to the journal of the backups, and nothing else.
/// </summary>
public sealed class BackupRolePrivilegesTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string InsufficientPrivilege = "42501";
    private const string CheckViolation = "23514";

    private const string JournalInsert = """
        INSERT INTO backup_run (base, step, started_at, finished_at, succeeded, bytes_read,
            bytes_added, repository_bytes, detail)
        VALUES ('lodb', 'backup', now() - interval '1 minute', now(), true, 4096, 1024,
            NULL, NULL)
        """;

    /// <summary>Writes, DDL and the server's powers, that the backups' role is refused.</summary>
    public static TheoryData<string> Refused => new()
    {
        "UPDATE backup_run SET succeeded = false",
        "DELETE FROM backup_run",
        "TRUNCATE backup_run",
        "INSERT INTO backup_run (id, step, started_at, finished_at, succeeded)"
            + " VALUES (1, 'check', now(), now(), true)",
        "INSERT INTO users (username, email, password, roles) VALUES ('x', 'x', 'x', '[]')",
        "UPDATE users SET username = 'intruder'",
        "DELETE FROM contact_messages",
        "CREATE TABLE intruder (id int)",
        "CREATE TEMP TABLE intruder (id int)",
        "SET ROLE lodb_app",
        "SELECT pg_read_file('PG_VERSION')",
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task BackupRoleWritesNothingButTheJournal(string sql)
    {
        var refusal = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsBackupAsync(sql));

        Assert.Equal(InsufficientPrivilege, refusal.SqlState);
    }

    [Fact]
    public async Task BackupRoleAppendsToTheJournalTheServerNumbersAndDates()
    {
        await ExecuteAsBackupAsync(JournalInsert);

        Assert.Equal(
            ["lodb backup t 4096 1024 t"],
            await Database.QueryAsync(
                "SELECT format('%s %s %s %s %s %s', base, step, succeeded, bytes_read,"
                    + " bytes_added, id > 0 AND recorded_at <= now()) FROM backup_run",
                Cancellation));
    }

    [Fact]
    public async Task JournalRefusesARowItsReaderCouldNotPlace()
    {
        var refusal = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsBackupAsync(
                "INSERT INTO backup_run (base, step, started_at, finished_at, succeeded)"
                    + " VALUES (NULL, 'backup', now(), now(), true)"));

        Assert.Equal(CheckViolation, refusal.SqlState);
    }

    [Fact]
    public async Task BackupRoleReadsEveryRowAndDumpsTheDatabase()
    {
        await Database.ExecuteAsync(
            "INSERT INTO periodic_job (name, last_started_at) VALUES ('backup-test', now())",
            Cancellation);

        var name = await QueryAsBackupAsync("SELECT name FROM periodic_job");
        var bytes = await Server.DumpAsAsync(Database.Name, DatabaseRoleNames.Backup, Cancellation);

        Assert.Equal("backup-test", name);
        Assert.True(bytes > 0);
    }

    [Fact]
    public async Task RightsGrantedByHandAreTakenBackByTheNextRun()
    {
        await Database.ExecuteAsync(
            "GRANT UPDATE ON users TO lodb_sauvegarde;"
                + " GRANT DELETE, UPDATE ON backup_run TO lodb_sauvegarde;"
                + " GRANT CREATE ON SCHEMA public TO lodb_sauvegarde",
            Cancellation);

        await Database.MigrateAsync(Cancellation);

        string[] takenBack =
        [
            "UPDATE users SET username = 'intruder'",
            "DELETE FROM backup_run",
            "CREATE TABLE intruder (id int)",
        ];
        foreach (var sql in takenBack)
        {
            var refusal = await Assert.ThrowsAsync<PostgresException>(
                () => ExecuteAsBackupAsync(sql));
            Assert.Equal(InsufficientPrivilege, refusal.SqlState);
        }
    }

    private async Task ExecuteAsBackupAsync(string sql)
    {
        await using var connection = await OpenAsBackupAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Cancellation);
    }

    private async Task<string?> QueryAsBackupAsync(string sql)
    {
        await using var connection = await OpenAsBackupAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (string?)await command.ExecuteScalarAsync(Cancellation);
    }

    private async Task<NpgsqlConnection> OpenAsBackupAsync()
    {
        var connection = new NpgsqlConnection(
            TestRoles.BackupConnectionString(Database.ConnectionString));
        await connection.OpenAsync(Cancellation);
        return connection;
    }
}
