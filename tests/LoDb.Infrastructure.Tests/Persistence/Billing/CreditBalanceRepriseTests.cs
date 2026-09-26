using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Infrastructure.Tests.Persistence.Billing;

/// <summary>
/// The lot 6 migration on a legacy database: every positive balance becomes a grant dated
/// from the migration, valid twelve months, and the legacy stack keeps spending, crediting
/// and deleting keys as before.
/// </summary>
public sealed class CreditBalanceRepriseTests(PostgresContainerFixture postgres)
{
    private const string Seed = """
        INSERT INTO users (id, email, username, roles, created_at, is_banned)
        VALUES (1, 'one@example.test', 'one', '[]', '2025-01-01 10:00:00', false),
               (2, 'two@example.test', 'two', '[]', '2025-01-01 10:00:00', false);
        INSERT INTO api_keys (id, name, key_hash, key_prefix, plan, credits_balance,
                              is_active, created_at, revoked_at, user_id)
        VALUES (1, 'paid', repeat('a', 64), 'lodb_aaaa', 'credits', 12500, true,
                '2025-02-01 10:00:00', NULL, 1),
               (2, 'free', repeat('b', 64), 'lodb_bbbb', 'free', 0, true,
                '2025-02-01 10:00:00', NULL, 2),
               (3, 'revoked', repeat('c', 64), 'lodb_cccc', 'monthly', 40, false,
                '2024-12-01 10:00:00', '2025-02-01 10:00:00', 1);
        """;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PositiveBalancesBecomeGrantsOfTheMigrationDay()
    {
        await using var database = await LegacyDatabaseAsync();
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        await database.MigrateAsync(Cancellation);

        await using var context = database.CreateContext();
        var grants = await context.ApiCreditGrants
            .AsNoTracking()
            .OrderBy(static grant => grant.ApiKeyId)
            .ToListAsync(Cancellation);
        Assert.Equal(
            [(1, 12_500L), (3, 40L)],
            grants.Select(static grant => (grant.ApiKeyId, grant.Requests)));
        Assert.All(grants, grant =>
        {
            Assert.Equal(ApiCreditGrantSource.Migration, grant.Source);
            Assert.InRange(grant.PurchasedAt, before, DateTimeOffset.UtcNow);
            Assert.Equal(
                grant.PurchasedAt.AddMonths(ApiCreditGrant.ValidityMonths),
                grant.ExpiresAt);
            Assert.Null(grant.StripeSessionId);
            Assert.Null(grant.ExpiredAt);
            Assert.Null(grant.ExpiredRequests);
        });
    }

    [Fact]
    public async Task EveryBalanceIsCoveredAfterTheMigration()
    {
        await using var database = await LegacyDatabaseAsync();

        await database.MigrateAsync(Cancellation);

        await using var context = database.CreateContext();
        var keys = await context.ApiKeys.AsNoTracking().ToListAsync(Cancellation);
        var grants = await context.ApiCreditGrants.AsNoTracking().ToListAsync(Cancellation);
        Assert.All(keys, key => Assert.Equal(
            0,
            ApiCreditFifo.Uncovered(
                key.CreditsBalance,
                grants.Where(grant => grant.ApiKeyId == key.Id))));
    }

    [Fact]
    public async Task LegacyStackKeepsWorkingOnTheKeys()
    {
        await using var database = await LegacyDatabaseAsync();
        await database.MigrateAsync(Cancellation);

        // The legacy writes: go-api's decrement, the webhook's top-up, a deleted account.
        await database.ExecuteAsync(
            """
            UPDATE api_keys SET credits_balance = credits_balance - 1
            WHERE id = 1 AND credits_balance > 0;
            UPDATE api_keys SET credits_balance = credits_balance + 5000,
                rate_limit_per_min = GREATEST(rate_limit_per_min, 60)
            WHERE id = 2;
            DELETE FROM users WHERE id = 1;
            """,
            Cancellation);

        Assert.Equal(
            ["2 5000"],
            await database.QueryAsync(
                "SELECT id || ' ' || credits_balance FROM api_keys ORDER BY id",
                Cancellation));
        Assert.Equal(
            ["0"],
            await database.QueryAsync("SELECT count(*) FROM api_credit_grants", Cancellation));
    }

    private async Task<TestDatabase> LegacyDatabaseAsync()
    {
        var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.CreateDoctrineSchemaAsync(Cancellation);
        await database.ExecuteAsync(Seed, Cancellation);
        return database;
    }
}
