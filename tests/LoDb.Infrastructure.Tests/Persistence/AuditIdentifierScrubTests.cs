using LoDb.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// The scrub of the journal's older entries: a failed sign-in loses the identifier typed, in
/// its details and in its target, and keeps the rest; no other entry changes.
/// </summary>
public sealed class AuditIdentifierScrubTests(PostgresContainerFixture postgres)
{
    private const string PreviousMigration = "20261010232259_QueryStatistics";

    private const string Seed = """
        INSERT INTO audit_log (occurred_at, actor_type, action, outcome, target, ip, route, meta)
        VALUES
            (now(), 'anonymous', 'user.login_failed', 'failure', 'zqx.bob@zqxmail.test',
             '198.51.100.77', '/api/account/login',
             '{"identifier": "zqx.bob@zqxmail.test", "reason": "invalid-credentials"}'),
            (now(), 'anonymous', 'user.login_failed', 'failure', NULL, NULL, NULL,
             '{"identifier_kind": "email", "identifier_digest": "ab12", "reason": "x"}'),
            (now(), 'user', 'build.create', 'success', 'zqx build', NULL, NULL,
             '{"identifier": "kept outside the sign-in"}');
        """;

    private const string Rows =
        "SELECT concat_ws('|', action, coalesce(target, '-'), meta::text) FROM audit_log"
        + " ORDER BY id";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FailedSignInsLoseTheIdentifierTyped()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await MigrateToAsync(database, PreviousMigration);
        await database.ExecuteAsync(Seed, Cancellation);

        await database.MigrateAsync(Cancellation);

        Assert.Equal(
            [
                "user.login_failed|-|{\"reason\": \"invalid-credentials\"}",
                "user.login_failed|-|{\"reason\": \"x\", \"identifier_kind\": \"email\","
                    + " \"identifier_digest\": \"ab12\"}",
                "build.create|zqx build|{\"identifier\": \"kept outside the sign-in\"}",
            ],
            await database.QueryAsync(Rows, Cancellation));
    }

    private static async Task MigrateToAsync(TestDatabase database, string migration)
    {
        await using var context = database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(migration, Cancellation);
    }
}
