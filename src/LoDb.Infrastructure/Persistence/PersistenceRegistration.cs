using LoDb.Infrastructure.Persistence.Baseline;
using LoDb.Infrastructure.Persistence.Ddragon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace LoDb.Infrastructure.Persistence;

/// <summary>
/// Registrations of the persistence zone: the database context and its data source.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host. The
/// connection string is read when the data source is first resolved.
/// </remarks>
public static class PersistenceRegistration
{
    public static IServiceCollection AddLoDbPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(LoDbDataSource.Create);
        services.AddDbContextFactory<LoDbDbContext>(static (provider, options) =>
            options.UseLoDb(provider.GetRequiredService<NpgsqlDataSource>()));
        services.TryAddSingleton<IDdragonAssetStore, DdragonAssetStore>();
        services.TryAddSingleton<IDdragonVersionStore, DdragonVersionStore>();
        services.TryAddScoped<DatabaseMigrator>();
        return services;
    }
}
