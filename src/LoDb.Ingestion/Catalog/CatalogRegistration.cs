using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Ingestion.Catalog;

/// <summary>
/// Registrations of the catalog zone: the in-memory catalog and the image resolution.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class CatalogRegistration
{
    public static IServiceCollection AddLoDbCatalog(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
