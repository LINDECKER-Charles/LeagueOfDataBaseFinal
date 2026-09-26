using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;
using Microsoft.Extensions.Logging;

namespace LoDb.Infrastructure.Tests.Audit;

/// <summary>
/// The journal records each event with the actor, address and route of its request, cuts
/// what exceeds a column, and never fails the audited action: an unavailable database
/// loses the event with a Critical log and no exception.
/// </summary>
public sealed class AuditLogTests(PostgresContainerFixture postgres) : MigratedDatabase(postgres)
{
    private const string RowsQuery = """
        SELECT concat_ws('|', to_char(occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS'),
            actor_type, coalesce(actor_id::text, 'NULL'), coalesce(actor, 'NULL'), action,
            outcome, coalesce(target_type, 'NULL'), coalesce(target_id, 'NULL'),
            coalesce(target, 'NULL'), coalesce(ip, 'NULL'), coalesce(route, 'NULL'),
            coalesce(meta::text, 'NULL'))
        FROM audit_log ORDER BY id
        """;

    // Nothing listens on port 1: the connection is refused at once.
    private const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=lodb;Username=lodb;Password=unused;Timeout=5";

    [Fact]
    public async Task EventIsRecordedWithTheActorAddressAndRouteOfItsRequest()
    {
        await using var harness = new AuditHarness(Database.ConnectionString);
        harness.Requests.HttpContext =
            AuditHarness.Request(AuditHarness.SignedIn(7, "moderator", Role.Admin));

        await harness.Journal.RecordAsync(
            new AuditEvent
            {
                Action = AuditAction.AdminUserBan,
                Target = new AuditTarget(AuditTargetType.User, "12", "spammer"),
                Meta = new Dictionary<string, object?> { ["reason"] = "spam", ["days"] = 7 },
            },
            Cancellation);

        Assert.Equal(
            [
                "2026-09-26 08:30:15|admin|7|moderator|admin.user_ban|success|user|12|spammer"
                    + $"|203.0.113.9|{AuditHarness.Route}|{{\"days\": 7, \"reason\": \"spam\"}}",
            ],
            await Database.QueryAsync(RowsQuery, Cancellation));
    }

    [Fact]
    public async Task AccountWithoutTheAdminRoleIsAUser()
    {
        await using var harness = new AuditHarness(Database.ConnectionString);
        harness.Requests.HttpContext = AuditHarness.Request(AuditHarness.SignedIn(3, "alice"));

        await harness.Journal.RecordAsync(Event(AuditAction.BuildCreate), Cancellation);

        Assert.StartsWith(
            "2026-09-26 08:30:15|user|3|alice|build.create|success|NULL|NULL|NULL|203.0.113.9|",
            Assert.Single(await Database.QueryAsync(RowsQuery, Cancellation)),
            StringComparison.Ordinal);
    }

    // On sign-in the request is still anonymous: the module names the account itself.
    [Fact]
    public async Task ActorOfTheEventWinsOverTheRequest()
    {
        await using var harness = new AuditHarness(Database.ConnectionString);
        harness.Requests.HttpContext = AuditHarness.Request(new());

        await harness.Journal.RecordAsync(
            Event(AuditAction.UserLogin) with { Actor = AuditActor.User(3, "alice") },
            Cancellation);

        Assert.StartsWith(
            "2026-09-26 08:30:15|user|3|alice|user.login|success|",
            Assert.Single(await Database.QueryAsync(RowsQuery, Cancellation)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutsideARequestTheActorIsAnonymous()
    {
        await using var harness = new AuditHarness(Database.ConnectionString);

        await harness.Journal.RecordAsync(Event(AuditAction.AdminLogsPurge), Cancellation);

        Assert.Equal(
            [
                "2026-09-26 08:30:15|anonymous|NULL|NULL|admin.logs_purge|success|NULL|NULL"
                    + "|NULL|NULL|NULL|NULL",
            ],
            await Database.QueryAsync(RowsQuery, Cancellation));
    }

    [Fact]
    public async Task ValuesLongerThanTheirColumnAreCut()
    {
        await using var harness = new AuditHarness(Database.ConnectionString);

        await harness.Journal.RecordAsync(
            Event(AuditAction.AdminBuildHide) with
            {
                Actor = AuditActor.Admin(7, new string('a', AuditLogEntry.ActorMaxLength + 1)),
                Target = new AuditTarget(
                    AuditTargetType.Build,
                    new string('i', AuditLogEntry.TargetIdMaxLength + 1),
                    new string('t', AuditLogEntry.TargetMaxLength + 1)),
            },
            Cancellation);

        Assert.Equal(
            [
                $"{AuditLogEntry.ActorMaxLength}|{AuditLogEntry.TargetIdMaxLength}"
                    + $"|{AuditLogEntry.TargetMaxLength}",
            ],
            await Database.QueryAsync(
                "SELECT concat_ws('|', length(actor), length(target_id), length(target))"
                    + " FROM audit_log",
                Cancellation));
    }

    [Fact]
    public async Task AbortedRequestKeepsTheTrailOfItsAction()
    {
        await using var harness = new AuditHarness(Database.ConnectionString);
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        await harness.Journal.RecordAsync(Event(AuditAction.AccountDelete), aborted.Token);

        Assert.Single(await Database.QueryAsync(RowsQuery, Cancellation));
    }

    [Fact]
    public async Task UnavailableDatabaseLosesTheEventWithoutFailingTheAction()
    {
        await using var harness = new AuditHarness(UnreachableDatabase);

        await harness.Journal.RecordAsync(Event(AuditAction.UserLogin), Cancellation);

        var failure = Assert.Single(harness.Records);
        Assert.Equal(
            ("audit.journal.write_failed", LogLevel.Critical),
            (failure.Id.Name, failure.Level));
        Assert.Equal("user.login", failure.GetStructuredStateValue("Action"));
        Assert.NotNull(failure.Exception);
    }

    private static AuditEvent Event(AuditAction action) => new() { Action = action };
}
