using LoDb.Ingestion.Catalog.Export;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Catalog.Reading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog;

/// <summary>
/// Registrations of the catalog zone: the in-memory catalog and the image resolution.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host. The
/// zone builds on the ingestion zone, whose stored datasets, manifest, on-demand ingestion
/// and <c>HybridCache</c> it reads through.
/// </remarks>
public static class CatalogRegistration
{
    public static IServiceCollection AddLoDbCatalog(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CatalogOptions>()
            .Bind(configuration.GetSection(CatalogOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<CatalogOptions>, CatalogOptionsValidator>());
        services.TryAddSingleton<CatalogCache>();
        services.TryAddSingleton<ICatalogReader, CatalogReader>();
        services.TryAddSingleton<IImageResolver, ImageResolver>();
        services.TryAddSingleton<CatalogExport>();
        return services;
    }
}
