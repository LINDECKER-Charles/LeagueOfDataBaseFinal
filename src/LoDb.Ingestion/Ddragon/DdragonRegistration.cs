using LoDb.Ingestion.Normalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Ingestion.Ddragon;

/// <summary>
/// Registrations of the Data Dragon zone: the Data Dragon and CommunityDragon clients.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// Both services fetch through the egress zone (<c>AddLoDbEgress</c>), which Program.cs
/// registers too; the zone has no settings of its own.
/// </remarks>
public static class DdragonRegistration
{
    public static IServiceCollection AddLoDbDdragon(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton<IDdragonClient, DdragonClient>();
        services.TryAddSingleton<IDdragonDatasets, DdragonDatasets>();
        return services;
    }
}
