using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Locks;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Baseline;
using LoDb.Infrastructure.Persistence.Ddragon;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// The persistence and jobs zones resolve as the API resolves them, without touching the
/// database, and name the setting they miss.
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
}
