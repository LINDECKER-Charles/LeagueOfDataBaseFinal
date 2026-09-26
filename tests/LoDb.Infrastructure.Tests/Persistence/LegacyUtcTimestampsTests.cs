using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// The UTC converter applies to the legacy <c>timestamp without time zone</c> columns of its
/// list, and to no other column.
/// </summary>
public sealed class LegacyUtcTimestampsTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    [Fact]
    public void ConverterAppliesToItsListOnly()
    {
        var columns = TimestampColumns();

        var converted = columns
            .Where(static column => column.Property.GetValueConverter() is not null)
            .Select(static column => (column.Table, column.Column))
            .Order();
        Assert.Equal(LegacyUtcTimestamps.Columns.Order(), converted);
        Assert.All(
            columns.Where(static column => column.Property.GetValueConverter() is not null),
            static column =>
            {
                Assert.Same(LegacyUtcTimestamps.Converter, column.Property.GetValueConverter());
                Assert.Equal(LegacyUtcTimestamps.ColumnType, column.Property.GetColumnType());
            });
        Assert.All(
            columns.Where(static column => column.Property.GetValueConverter() is null),
            static column => Assert.EndsWith(
                " with time zone",
                column.Property.GetColumnType(),
                StringComparison.Ordinal));
    }

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

    [Fact]
    public async Task TimestampTzColumnKeepsItsInstant()
    {
        var discovered = new DateTimeOffset(2026, 9, 26, 8, 30, 15, TimeSpan.Zero)
            .AddTicks(1_234_560);
        await using (var context = Database.CreateContext())
        {
            context.DdragonVersions.Add(new DdragonVersion
            {
                Version = "16.19.1",
                Status = DdragonVersionStatus.Discovered,
                DiscoveredAt = discovered,
                UpdatedAt = discovered,
            });
            await context.SaveChangesAsync(Cancellation);
        }

        Assert.Equal(
            ["2026-09-26 08:30:15.123456+00"],
            await Database.QueryAsync(
                "SELECT discovered_at::text FROM ddragon_version",
                Cancellation));
        await using var reader = Database.CreateContext();
        var stored = await reader.DdragonVersions.AsNoTracking()
            .Select(static version => version.DiscoveredAt)
            .SingleAsync(Cancellation);
        Assert.Equal(discovered, stored);
        Assert.Equal(TimeSpan.Zero, stored.Offset);
    }

    private static User NewUser(DateTimeOffset createdAt) => new()
    {
        Email = "utc@example.test",
        Username = "utc",
        Roles = [],
        CreatedAt = createdAt,
    };

    // Every DateTimeOffset of the model, nullable or not, with its table and column.
    private static List<(string Table, string Column, IReadOnlyProperty Property)>
        TimestampColumns()
    {
        using var context = new LoDbDesignTimeFactory().CreateDbContext([]);
        var model = context.GetService<IDesignTimeModel>().Model;
        return
        [
            .. from entity in model.GetEntityTypes()
               from property in entity.GetProperties()
               let type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType
               where type == typeof(DateTimeOffset)
               select (
                   entity.GetTableName()!,
                   property.GetColumnName(),
                   (IReadOnlyProperty)property),
        ];
    }
}
