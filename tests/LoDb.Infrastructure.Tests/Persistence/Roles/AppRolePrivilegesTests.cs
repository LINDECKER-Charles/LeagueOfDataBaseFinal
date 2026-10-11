using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Infrastructure.Persistence.Contact;
using LoDb.Infrastructure.Persistence.Roles;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.Roles;

/// <summary>
/// What the API's role may do on a migrated database: rows, the daily partitions through
/// their two functions, and nothing else; and who owns what.
/// </summary>
public sealed class AppRolePrivilegesTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string InsufficientPrivilege = "42501";

    private const string OwnersSql = """
        SELECT DISTINCT pg_get_userbyid(c.relowner) || ' '
            || (c.relname LIKE 'analytics\_event%')::text
        FROM pg_class c
        WHERE c.relnamespace = 'public'::regnamespace
            AND c.relkind IN ('r', 'p', 'v', 'm', 'f', 'S')
            AND NOT EXISTS (
                SELECT FROM pg_depend d
                WHERE d.classid = 'pg_class'::regclass AND d.objid = c.oid
                    AND d.deptype = 'e')
        ORDER BY 1
        """;

    private const string FunctionsSql = """
        SELECT format('%s %s %s', p.proname, pg_get_userbyid(p.proowner), p.proacl::text)
        FROM pg_proc p
        WHERE p.pronamespace = 'public'::regnamespace AND p.proname LIKE 'lodb\_%'
        ORDER BY 1
        """;

    private static readonly DateOnly Day = new(2030, 1, 1);

    private NpgsqlDataSource? _app;

    private NpgsqlDataSource App => _app ??= NpgsqlDataSource.Create(Database.AppConnectionString);

    /// <summary>DDL, and the server's own powers, that the API's role is refused.</summary>
    public static TheoryData<string> Refused => new()
    {
        "CREATE TABLE intruder (id int)",
        "CREATE TEMP TABLE intruder (id int)",
        "CREATE FUNCTION intruder() RETURNS int LANGUAGE sql AS 'SELECT 1'",
        "CREATE INDEX intruder ON users (email)",
        "ALTER TABLE users ADD COLUMN intruder int",
        "DROP TABLE users",
        "TRUNCATE users",
        "INSERT INTO \"__EFMigrationsHistory\" VALUES ('intruder', '0')",
        "CREATE TABLE analytics_event_20300101 PARTITION OF analytics_event"
            + " FOR VALUES FROM ('2030-01-01') TO ('2030-01-02')",
        "SET ROLE lodb_partitions",
        "ALTER ROLE lodb_app CREATEDB",
        "COPY users TO PROGRAM 'id'",
        "SELECT pg_read_file('PG_VERSION')",
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task ApiRoleCannotChangeTheSchemaNorReachTheServer(string sql)
    {
        await using var command = App.CreateCommand(sql);

        var refusal = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync(Cancellation));

        Assert.Equal(InsufficientPrivilege, refusal.SqlState);
    }

    [Fact]
    public async Task ApiRoleReadsAndWritesRows()
    {
        await using var db = CreateAppContext();
        var message = new ContactMessage
        {
            Category = "bug",
            Email = "player@example.test",
            Message = "A broken page.",
            Status = "new",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.ContactMessages.Add(message);
        await db.SaveChangesAsync(Cancellation);

        message.Status = "handled";
        await db.SaveChangesAsync(Cancellation);
        var stored = await db.ContactMessages.AsNoTracking().SingleAsync(Cancellation);
        db.ContactMessages.Remove(message);
        await db.SaveChangesAsync(Cancellation);

        Assert.Equal("handled", stored.Status);
        Assert.Empty(await db.ContactMessages.ToListAsync(Cancellation));
        Assert.NotEmpty(await db.Database
            .SqlQueryRaw<string>("SELECT migration_id AS \"Value\" FROM \"__EFMigrationsHistory\"")
            .ToListAsync(Cancellation));
    }

    [Fact]
    public async Task ApiRoleCreatesAndDropsTheDailyPartitionsThroughTheFunctions()
    {
        var partitions = new AnalyticsPartitions(App);

        var created = await partitions.CreateAsync(Day, Day.AddDays(1), Cancellation);
        var listed = await partitions.ListAsync(Cancellation);
        var owners = await Database.QueryAsync(
            "SELECT DISTINCT pg_get_userbyid(relowner) FROM pg_class"
                + " WHERE relname LIKE 'analytics\\_event\\_2030%'",
            Cancellation);
        var dropped = await partitions.DropBeforeAsync(Day.AddDays(2), Cancellation);

        Assert.Equal(2, created);
        Assert.Contains(Day, listed);
        Assert.Equal([DatabaseRoleNames.Partitions], owners);
        Assert.Equal(2, dropped);
        Assert.DoesNotContain(Day, await partitions.ListAsync(Cancellation));
    }

    [Fact]
    public async Task DropFunctionLeavesATableThatIsNoPartition()
    {
        var name = AnalyticsPartitionNames.Of(Day);
        await Database.ExecuteAsync($"CREATE TABLE {name} (id int)", Cancellation);
        await using var command = App.CreateCommand(
            "SELECT public.lodb_drop_analytics_partition(@day)");
        command.Parameters.AddWithValue("day", Day);

        var dropped = await command.ExecuteScalarAsync(Cancellation);

        Assert.Equal(false, dropped);
        Assert.Contains(name, await Database.TablesAsync(Cancellation));
    }

    [Fact]
    public async Task EveryObjectBelongsToItsOwnerAndTheFunctionsToTheApiAlone()
    {
        await new AnalyticsPartitions(App).CreateAsync(Day, Day, Cancellation);

        var owners = await Database.QueryAsync(OwnersSql, Cancellation);
        var functions = await Database.QueryAsync(FunctionsSql, Cancellation);

        Assert.Equal(["lodb_owner false", "lodb_partitions true"], owners);
        Assert.Equal(
            [
                "lodb_create_analytics_partition lodb_partitions"
                    + " {lodb_partitions=X/lodb_partitions,lodb_app=X/lodb_partitions}",
                "lodb_drop_analytics_partition lodb_partitions"
                    + " {lodb_partitions=X/lodb_partitions,lodb_app=X/lodb_partitions}",
            ],
            functions);
    }

    [Fact]
    public async Task TableOfALaterMigrationIsTheApisAtOnceThenGoesToItsOwner()
    {
        await Database.ExecuteAsync(
            "CREATE TABLE later_feature (id int GENERATED BY DEFAULT AS IDENTITY, name text)",
            Cancellation);
        await using (var insert = App.CreateCommand(
            "INSERT INTO later_feature (name) VALUES ('first')"))
        {
            await insert.ExecuteNonQueryAsync(Cancellation);
        }

        await Database.MigrateAsync(Cancellation);

        Assert.Equal(
            ["lodb_owner"],
            await Database.QueryAsync(
                "SELECT pg_get_userbyid(relowner) FROM pg_class WHERE relname = 'later_feature'",
                Cancellation));
    }

    [Fact]
    public async Task StatementTimeoutIsTheRolesUnlessAJobRaisesIt()
    {
        await using var connection = await App.OpenConnectionAsync(Cancellation);
        var initial = await ShowTimeoutAsync(connection, null);
        await using (var transaction = await connection.BeginTransactionAsync(Cancellation))
        {
            await using var job = new NpgsqlCommand("SELECT 1", connection);
            await StatementTimeouts.RaiseAsync(
                transaction,
                job,
                TimeSpan.FromMinutes(5),
                Cancellation);
            Assert.Equal("5min", await ShowTimeoutAsync(connection, transaction));
            Assert.Equal(300, job.CommandTimeout);
            await transaction.CommitAsync(Cancellation);
        }

        Assert.Equal("15s", initial);
        Assert.Equal("15s", await ShowTimeoutAsync(connection, null));
    }

    [Fact]
    public async Task OnlyASuperuserKeepsTheRoles()
    {
        await using var connection = await App.OpenConnectionAsync(Cancellation);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestRoles.Create().ApplyAsync(connection, Cancellation));
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private LoDbDbContext CreateAppContext()
    {
        var options = new DbContextOptionsBuilder<LoDbDbContext>();
        options.UseLoDb(App);
        return new LoDbDbContext(options.Options);
    }

    private static async Task<string> ShowTimeoutAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        await using var command = new NpgsqlCommand(
            "SHOW statement_timeout",
            connection,
            transaction);
        return (string)(await command.ExecuteScalarAsync(Cancellation))!;
    }
}
