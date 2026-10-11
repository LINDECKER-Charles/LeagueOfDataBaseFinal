using LoDb.Infrastructure.Persistence.Billing.Payments;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.Billing;

/// <summary>
/// The payment states come back as they were saved, their enumerations stored with the
/// spelling the check constraints list; a payment names one session at most; and
/// <c>tools/db/anonymize.sql</c> turns their Stripe identifiers into pseudonyms.
/// </summary>
public sealed class PaymentStateTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    // UTC and whole microseconds: what timestamptz keeps.
    private static readonly DateTimeOffset At =
        new DateTimeOffset(2026, 10, 11, 8, 30, 15, TimeSpan.Zero).AddTicks(1_234_560);

    [Fact]
    public async Task StripeCheckoutRoundTrips()
    {
        await AssertRoundTripsAsync(Checkout("cs_test_a1B2c3D4", "pi_test_a1B2c3D4"));

        Assert.Equal(
            ["api_pack|revoked"],
            await Database.QueryAsync(
                "SELECT concat_ws('|', kind, status) FROM stripe_checkout",
                Cancellation));
    }

    [Fact]
    public async Task StripeSubscriptionRoundTrips()
    {
        await AssertRoundTripsAsync(new StripeSubscription
        {
            Id = "sub_test_a1B2c3D4",
            Plan = "annual_plus",
            Status = StripeSubscriptionStatus.Suspended,
            ChangedAt = At,
        });

        Assert.Equal(
            ["suspended"],
            await Database.QueryAsync("SELECT status FROM stripe_subscription", Cancellation));
    }

    [Fact]
    public async Task PaymentNamesOneSessionAtMost()
    {
        await using var context = Database.CreateContext();
        context.AddRange(
            Checkout("cs_test_plan_1", paymentIntentId: null),
            Checkout("cs_test_plan_2", paymentIntentId: null),
            Checkout("cs_test_first", "pi_test_shared"));
        await context.SaveChangesAsync(Cancellation);
        context.Add(Checkout("cs_test_second", "pi_test_shared"));

        var error = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(Cancellation));

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task AnonymizationTurnsStripeIdentifiersIntoPseudonyms()
    {
        await Database.ExecuteAsync(
            """
            INSERT INTO stripe_checkout (id, kind, status, payment_intent_id, amount_cents,
                                         currency, created_at, updated_at)
            VALUES ('cs_live_zqx1', 'api_pack', 'revoked', 'pi_zqx1', 500, 'eur', now(), now()),
                   ('cs_live_zqx2', 'api_plan', 'paid', NULL, 4800, 'eur', now(), now());
            INSERT INTO stripe_subscription (id, plan, status, changed_at)
            VALUES ('sub_zqx1', 'annual', 'suspended', now());
            """,
            Cancellation);

        await AnonymizationTests.AnonymizeAsync(Database, AnonymizationTests.PasswordHash);

        Assert.Equal(
            ["t|api_pack|revoked|t|500|eur", "t|api_plan|paid|NULL|4800|eur"],
            await Database.QueryAsync(
                """
                SELECT concat_ws('|', id ~ '^cs_anon_[0-9a-f]{24}$', kind, status,
                    CASE WHEN payment_intent_id IS NULL THEN 'NULL'
                        WHEN payment_intent_id ~ '^pi_anon_[0-9a-f]{24}$' THEN 't' END,
                    amount_cents, currency)
                FROM stripe_checkout ORDER BY kind
                """,
                Cancellation));
        Assert.Equal(
            ["t|annual|suspended"],
            await Database.QueryAsync(
                """
                SELECT concat_ws('|', id ~ '^sub_anon_[0-9a-f]{24}$', plan, status)
                FROM stripe_subscription
                """,
                Cancellation));
    }

    private static StripeCheckout Checkout(string id, string? paymentIntentId) => new()
    {
        Id = id,
        Kind = StripeCheckoutKind.Pack,
        Status = StripeCheckoutStatus.Revoked,
        PaymentIntentId = paymentIntentId,
        AmountCents = 1_000,
        Currency = "eur",
        CreatedAt = At.AddMinutes(-5),
        UpdatedAt = At,
    };

    private async Task AssertRoundTripsAsync<T>(T expected)
        where T : class
    {
        await using (var context = Database.CreateContext())
        {
            context.Add(expected);
            await context.SaveChangesAsync(Cancellation);
        }

        await using var reader = Database.CreateContext();
        var actual = await reader.Set<T>().AsNoTracking().SingleAsync(Cancellation);
        Assert.Equivalent(expected, actual, strict: true);
    }
}
