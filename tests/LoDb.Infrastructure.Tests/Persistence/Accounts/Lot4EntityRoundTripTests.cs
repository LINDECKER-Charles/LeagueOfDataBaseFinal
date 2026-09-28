using LoDb.Domain.Languages;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Infrastructure.Persistence.Outbox;
using LoDb.Testing;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Infrastructure.Tests.Persistence.Accounts;

/// <summary>
/// Every entity of lot 4 comes back as it was saved, every column filled, and enumerations
/// are stored with the spelling the raw SQL and the legacy journal use.
/// </summary>
public sealed class Lot4EntityRoundTripTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    // UTC and whole microseconds: what timestamptz keeps.
    private static readonly DateTimeOffset At =
        new DateTimeOffset(2026, 9, 26, 8, 30, 15, TimeSpan.Zero).AddTicks(1_234_560);

    [Fact]
    public async Task RoleRoundTrips() =>
        await AssertRoundTripsAsync(new Role
        {
            Name = Role.Admin,
            NormalizedName = "ADMIN",
            ConcurrencyStamp = "0b7d6c2a-1e4f-4a3b-8c9d-5e6f7a8b9c0d",
        });

    [Fact]
    public async Task UserRoleRoundTrips()
    {
        var user = await SeedUserAsync();
        var role = await SeedAsync(new Role { Name = Role.Admin, NormalizedName = "ADMIN" });

        await AssertRoundTripsAsync(new IdentityUserRole<int> { UserId = user, RoleId = role.Id });
    }

    [Fact]
    public async Task UserTokenRoundTrips()
    {
        var user = await SeedUserAsync();

        await AssertRoundTripsAsync(new IdentityUserToken<int>
        {
            UserId = user,
            LoginProvider = "[AspNetUserStore]",
            Name = "AuthenticatorKey",
            Value = "JBSWY3DPEHPK3PXP",
        });
    }

    [Fact]
    public async Task DataProtectionKeyRoundTrips() =>
        await AssertRoundTripsAsync(new DataProtectionKey
        {
            FriendlyName = "key-3f2a9c1e",
            Xml = """<key id="3f2a9c1e" version="1" />""",
        });

    [Fact]
    public async Task EmailOutboxMessageRoundTrips()
    {
        await AssertRoundTripsAsync(new EmailOutboxMessage
        {
            Recipient = "player@example.test",
            Template = EmailTemplate.ResetPassword,
            Locale = UiLocale.ZhHant,
            Model = """{"link": "https://example.test/reset/abc", "userName": "player"}""",
            Status = EmailOutboxStatus.Dead,
            Attempts = 8,
            NextAttemptAt = At.AddHours(1),
            CreatedAt = At,
            LastAttemptAt = At.AddMinutes(30),
            SentAt = At.AddMinutes(31),
            LastErrorCode = "smtp.550",
        });
        Assert.Equal(
            ["reset_password|zh-hant|dead"],
            await Database.QueryAsync(
                "SELECT concat_ws('|', template, locale, status) FROM email_outbox",
                Cancellation));
    }

    [Fact]
    public async Task AuditLogEntryRoundTrips()
    {
        await AssertRoundTripsAsync(new AuditLogEntry
        {
            OccurredAt = At,
            ActorType = AuditActorType.Admin,
            ActorId = 7,
            Actor = "moderator",
            Action = AuditAction.AdminApiClientCredit,
            Outcome = AuditOutcome.Denied,
            TargetType = AuditTargetType.ApiClient,
            TargetId = "42",
            Target = "lodb_ab12…",
            Ip = "2001:db8::7",
            Route = "/api/admin/api-clients/{id}/credit",
            Meta = """{"requests": 5000}""",
        });
        Assert.Equal(
            ["admin|admin.api_client_credit|denied|api_client"],
            await Database.QueryAsync(
                "SELECT concat_ws('|', actor_type, action, outcome, target_type) FROM audit_log",
                Cancellation));
    }

    private async Task<int> SeedUserAsync() =>
        (await SeedAsync(new User
        {
            Email = "owner@example.test",
            UserName = "owner",
            Roles = [],
            CreatedAt = At.AddTicks(-1_234_560),
        })).Id;

    // In a context of its own, so that the entity read next comes from the database.
    private async Task<T> SeedAsync<T>(T entity)
        where T : class
    {
        await using var context = Database.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync(Cancellation);
        return entity;
    }

    private async Task AssertRoundTripsAsync<T>(T expected)
        where T : class
    {
        await SeedAsync(expected);

        await using var context = Database.CreateContext();
        var actual = await context.Set<T>().AsNoTracking().SingleAsync(Cancellation);
        Assert.Equivalent(expected, actual, strict: true);
    }
}
