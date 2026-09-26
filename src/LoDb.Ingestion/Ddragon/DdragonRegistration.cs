using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Ingestion.Ddragon;

/// <summary>
/// Registrations of the Data Dragon zone: the Data Dragon and CommunityDragon clients.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class DdragonRegistration
{
    public static IServiceCollection AddLoDbDdragon(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
