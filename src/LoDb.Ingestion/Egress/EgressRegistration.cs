using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Ingestion.Egress;

/// <summary>
/// Registrations of the egress zone: the filtered outbound HTTP client of the ingestion.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class EgressRegistration
{
    public static IServiceCollection AddLoDbEgress(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
