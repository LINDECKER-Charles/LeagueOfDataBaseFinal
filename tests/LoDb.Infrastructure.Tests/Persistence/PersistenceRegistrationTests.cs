using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Locks;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Infrastructure.Persistence.Baseline;
using LoDb.Infrastructure.Persistence.Ddragon;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// The persistence and jobs zones resolve as the API resolves them, without touching the
/// database, and name the setting they miss; their stores answer what needs no database
/// without connecting, and the model converts the legacy timestamps it lists.
/// </summary>
public sealed class PersistenceRegistrationTests
{
    // Nothing listens there: resolving must not connect.
    private const string Unreachable =
        "Host=127.0.0.1;Port=1;Username=nobody;Password=none;Database=none";

    [Fact]
    public void EveryServiceResolvesWithoutConnecting()
    {
        using var services = PersistenceServices.Build(Unreachable, TimeProvider.System);
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        Assert.NotNull(provider.GetRequiredService<LoDbDbContext>());
        Assert.NotNull(provider.GetRequiredService<IDbContextFactory<LoDbDbContext>>());
        Assert.NotNull(provider.GetRequiredService<DatabaseMigrator>());
        Assert.NotNull(provider.GetRequiredService<IDdragonAssetStore>());
        Assert.NotNull(provider.GetRequiredService<IDdragonVersionStore>());
        Assert.NotNull(provider.GetRequiredService<IAnalyticsPartitions>());
        Assert.NotNull(provider.GetRequiredService<IAnalyticsEventWriter>());
        Assert.IsType<PostgresDistributedLock>(provider.GetRequiredService<IDistributedLock>());
        Assert.NotNull(provider.GetRequiredService<IJobSchedule>());
        Assert.NotNull(provider.GetRequiredService<JobMetrics>());
        Assert.NotNull(provider.GetRequiredService<PeriodicJobServices>());
    }

    [Fact]
    public void ContextUsesTheSharedDataSource()
    {
        using var services = PersistenceServices.Build(Unreachable, TimeProvider.System);
        using var scope = services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<LoDbDbContext>();
        var dataSource = services.GetRequiredService<NpgsqlDataSource>();

        Assert.Equal(dataSource.ConnectionString, context.Database.GetConnectionString());
        Assert.Contains("Port=1", dataSource.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingConnectionStringIsNamedOnFirstUse()
    {
        using var services = PersistenceServices.Build(null, TimeProvider.System);

        var dataSource = Assert.Throws<InvalidOperationException>(
            () => services.GetRequiredService<NpgsqlDataSource>());
        var distributedLock = Assert.Throws<InvalidOperationException>(
            () => services.GetRequiredService<IDistributedLock>());

        Assert.Equal("ConnectionStrings:LoDb is not set.", dataSource.Message);
        Assert.Equal("ConnectionStrings:LoDb is not set.", distributedLock.Message);
    }

    // An empty batch and a reversed range are settled before any connection: the database
    // here would fail the call.
    [Fact]
    public async Task EmptyBatchesAndReversedRangesNeverConnect()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var day = new DateOnly(2026, 9, 26);
        await using var services = PersistenceServices.Build(Unreachable, TimeProvider.System);
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var assets = await provider.GetRequiredService<IDdragonAssetStore>()
            .RecordAsync([], force: true, cancellation);
        await provider.GetRequiredService<IAnalyticsEventWriter>().WriteAsync([], cancellation);

        Assert.Equal(0, assets);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            provider.GetRequiredService<IAnalyticsPartitions>()
                .CreateAsync(day, day.AddDays(-1), cancellation));
    }

    [Fact]
    public void LegacyUtcConverterAppliesToItsListOnly()
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
