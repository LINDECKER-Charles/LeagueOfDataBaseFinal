using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// A legacy <c>timestamp without time zone</c> column holds the UTC wall clock, written and
/// read back as UTC.
/// </summary>
/// <remarks>
/// Which columns the converter applies to is read from the model alone, in
/// <see cref="PersistenceRegistrationTests"/>.
/// </remarks>
public sealed class LegacyUtcTimestampsTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    [Fact]
    public async Task OffsetValueIsStoredAsTheUtcWallClockTruncatedToTheSecond()
    {
        var local = new DateTimeOffset(2026, 9, 26, 10, 30, 15, TimeSpan.FromHours(2))
            .AddMilliseconds(987);
        await using (var context = Database.CreateContext())
        {
            context.Users.Add(NewUser(local));
            await context.SaveChangesAsync(Cancellation);
        }

        Assert.Equal(
            ["2026-09-26 08:30:15"],
            await Database.QueryAsync("SELECT created_at::text FROM users", Cancellation));
        await using var reader = Database.CreateContext();
        var stored = await reader.Users.AsNoTracking().Select(static user => user.CreatedAt)
            .SingleAsync(Cancellation);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 8, 30, 15, TimeSpan.Zero), stored);
        Assert.Equal(TimeSpan.Zero, stored.Offset);
    }

    [Fact]
    public async Task LegacyRowIsReadAsUtc()
    {
        await Database.ExecuteAsync(
            """
            INSERT INTO users (email, username, roles, created_at)
            VALUES ('legacy@example.test', 'legacy', '[]', '2026-01-02 03:04:05')
            """,
            Cancellation);
        var expected = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var sameInstant = expected.ToOffset(TimeSpan.FromHours(2));

        await using var context = Database.CreateContext();
        var stored = await context.Users.AsNoTracking().Select(static user => user.CreatedAt)
            .SingleAsync(Cancellation);
        var matching = await context.Users.CountAsync(
            user => user.CreatedAt == sameInstant,
            Cancellation);

        Assert.Equal(expected, stored);
        Assert.Equal(TimeSpan.Zero, stored.Offset);
        Assert.Equal(1, matching);
    }

    private static User NewUser(DateTimeOffset createdAt) => new()
    {
        Email = "utc@example.test",
        UserName = "utc",
        Roles = [],
        CreatedAt = createdAt,
    };
}
