using LoDb.Ingestion.Images;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Ingestion.Pipeline.Watch;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Pipeline;

/// <summary>
/// Registrations of the ingestion zone: patch watch, version ingestion and on-demand queue.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host. The
/// zone builds on the egress, storage, persistence, jobs and Data Dragon zones, and on the
/// host's metrics; the periodic job and the workers are registered by the API's convention.
/// </remarks>
public static class IngestionRegistration
{
    public static IServiceCollection AddLoDbIngestion(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<IngestionOptions>()
            .Bind(configuration.GetSection(IngestionOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<IngestionOptions>, IngestionOptionsValidator>());

        // The version and language lists; the catalog zone caches through it too.
        services.AddHybridCache();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IngestionMetrics>();
        services.TryAddSingleton<KnownReleases>();
        services.TryAddSingleton<StoredDatasets>();
        services.TryAddSingleton<DatasetIngestion>();
        services.TryAddSingleton<ImageManifest>();
        services.TryAddSingleton<ImageIngestion>();
        services.TryAddSingleton<VersionStates>();
        services.TryAddSingleton<IVersionIngestion, VersionIngestion>();
        services.TryAddSingleton<IVersionBacklog, VersionBacklog>();
        services.TryAddSingleton<IPatchWatch, PatchWatch>();
        AddOnDemand(services);
        return services;
    }

    // One instance behind both faces: the callers' and the worker's.
    private static void AddOnDemand(IServiceCollection services)
    {
        services.TryAddSingleton<CrawlerBudget>();
        services.TryAddSingleton<OnDemandIngestion>();
        services.TryAddSingleton<IOnDemandIngestion>(
            static provider => provider.GetRequiredService<OnDemandIngestion>());
        services.TryAddSingleton<IOnDemandBacklog>(
            static provider => provider.GetRequiredService<OnDemandIngestion>());
    }
}
