using System.Globalization;
using System.Text.Json;
using LoDb.Api.Cli;
using LoDb.Api.Modules.Audit;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Audit;

/// <summary>
/// <c>audit import</c> over a sample of the legacy journal: the local copy wins over the
/// archive, days past the retention and broken lines stay out, and a second run adds nothing.
/// </summary>
/// <remarks>
/// The lines are those <c>AuditEvent::toArray</c> writes; their days follow the real clock,
/// which the command's host reads, so that they stay within the retention.
/// </remarks>
public sealed class AuditImportCommandTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string DayFormat = "yyyy-MM-dd";
    private const string AtomFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    private readonly DirectoryInfo _local = Directory.CreateTempSubdirectory("lodb-audit-local-");
    private readonly DirectoryInfo _archive =
        Directory.CreateTempSubdirectory("lodb-audit-archive-");
    private TestDatabase? _database;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static DateTime Today => DateTime.UtcNow.Date;

    private TestDatabase Database =>
        _database ?? throw new InvalidOperationException("The database is not created yet.");

    [Fact]
    public void ApiDeclaresTheAuditImportCommand()
    {
        var catalog = CliCommandCatalog.Discover(typeof(Program).Assembly);

        var command = Assert.Single(catalog.Commands, static c => c.Name == "audit import");
        Assert.True(command.RequiresHost);
    }

    [Fact]
    public async Task SampleIsImportedWithinTheRetention()
    {
        WriteSample();

        var exitCode = await RunAsync("--source", _local.FullName, "--source", _archive.FullName);

        Assert.Equal(CliExitCodes.Success, exitCode);
        var journal = await JournalAsync();
        Assert.Equal(
            [AuditAction.UserLoginFailed, AuditAction.AdminUserBan, AuditAction.UserLogin,
                AuditAction.BuildCreate],
            journal.Select(static entry => entry.Action));
        var failed = journal[0];
        Assert.Equal(
            (AuditActorType.Anonymous, (int?)null, (string?)null, AuditOutcome.Failure),
            (failed.ActorType, failed.ActorId, failed.Actor, failed.Outcome));
        Assert.Equal("legende@example.test", AccountsApp.Meta(failed, "identifier"));
        var ban = journal[1];
        Assert.Equal(
            (AuditActorType.Admin, (int?)null, "admin", AuditTargetType.User, "42"),
            (ban.ActorType, ban.ActorId, ban.Actor, ban.TargetType, ban.TargetId));
        Assert.Equal("app_admin_user_ban", ban.Route);
        Assert.Equal(("Legende_42", 42), (journal[2].Actor, journal[2].ActorId));
        Assert.Equal("203.0.113.5", journal[2].Ip);
    }

    [Fact]
    public async Task SecondRunAddsNothing()
    {
        WriteSample();
        await RunAsync("--source", _local.FullName, "--source", _archive.FullName);

        var exitCode = await RunAsync("--source", _local.FullName, "--source", _archive.FullName);

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(4, (await JournalAsync()).Count);
    }

    [Fact]
    public async Task DryRunWritesNothing()
    {
        WriteSample();

        var exitCode = await RunAsync("--source", _local.FullName, "--dry-run");

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Empty(await JournalAsync());
    }

    [Fact]
    public async Task MissingDirectoryFails()
    {
        var exitCode = await RunAsync("--source", Path.Combine(_local.FullName, "absent"));

        Assert.Equal(CliExitCodes.Failure, exitCode);
    }

    [Fact]
    public async Task MissingSourceIsAUsageError()
    {
        Assert.Equal(CliExitCodes.Usage, await RunAsync("--dry-run"));
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

        _local.Delete(recursive: true);
        _archive.Delete(recursive: true);
    }

    // Settings first: a bare --dry-run last never takes one for its value.
    private Task<int> RunAsync(params string[] arguments) =>
        CliRunner.RunAsync(
            [
                "audit", "import",
                $"--ConnectionStrings:LoDb={Database.ConnectionString}",
                "--Logging:LogLevel:Default=Warning",
                .. arguments,
            ],
            typeof(Program).Assembly,
            static (services, configuration) =>
                services.AddLoDbPersistence(configuration).AddAudit(configuration));

    private async Task<List<AuditLogEntry>> JournalAsync()
    {
        await using var context = Database.CreateContext();
        return await context.AuditLog.AsNoTracking()
            .OrderBy(static entry => entry.OccurredAt)
            .ToListAsync(Cancellation);
    }

    // Three days: one past the retention, one both local and archived (the local copy is the
    // longer one), one archived only; broken lines in between.
    private void WriteSample()
    {
        var expired = Today.AddMonths(-7);
        var shared = Today.AddDays(-20);
        var archived = Today.AddDays(-3);
        Write(_archive, expired, Line(expired, "user.login", "user", "42", "Legende_42"));
        Write(_archive, shared, Failed(shared));
        Write(
            _local,
            shared,
            Failed(shared),
            "{not json",
            """{"at":"2026-01-01T00:00:00+00:00","action":"user.dance"}""",
            Ban(shared.AddHours(2)));
        Write(
            _archive,
            archived,
            Line(archived, "user.login", "user", "42", "Legende_42"),
            string.Empty,
            Line(archived.AddHours(1), "build.create", "user", "42", "Legende_42"));
    }

    private static void Write(DirectoryInfo directory, DateTime day, params string[] lines)
    {
        var name = day.ToString(DayFormat, CultureInfo.InvariantCulture) + ".ndjson";
        File.WriteAllLines(Path.Combine(directory.FullName, name), lines);
    }

    private static string Line(DateTime at, string action, string type, string id, string actor) =>
        JsonSerializer.Serialize(new
        {
            at = Atom(at),
            actorType = type,
            actorId = id,
            actor,
            action,
            outcome = "success",
            targetType = (string?)null,
            targetId = (string?)null,
            target = (string?)null,
            ip = "203.0.113.5",
            route = "app_login",
            meta = (object?)null,
        });

    private static string Failed(DateTime at) =>
        JsonSerializer.Serialize(new
        {
            at = Atom(at),
            actorType = "anonymous",
            actorId = (string?)null,
            actor = "anonyme",
            action = "user.login_failed",
            outcome = "failure",
            target = "legende@example.test",
            ip = "203.0.113.5",
            route = "app_login",
            meta = new { identifier = "legende@example.test" },
        });

    // The legacy operator: an account-less "admin" whose id is its name.
    private static string Ban(DateTime at) =>
        JsonSerializer.Serialize(new
        {
            at = Atom(at),
            actorType = "admin",
            actorId = "admin",
            actor = "admin",
            action = "admin.user_ban",
            outcome = "success",
            targetType = "user",
            targetId = 42,
            target = "Legende_42",
            ip = "198.51.100.1",
            route = "app_admin_user_ban",
            meta = new { reason = "spam" },
        });

    private static string Atom(DateTime at) =>
        new DateTimeOffset(at, TimeSpan.Zero).ToString(AtomFormat, CultureInfo.InvariantCulture);
}
