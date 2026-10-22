using LoDb.Ingestion.Catalog.States.Loading;
using LoDb.Ingestion.Catalog.States.Promotion;
using LoDb.Ingestion.Catalog.States.Storage;
using LoDb.Ingestion.Catalog.States.Telemetry;
using LoDb.Ingestion.Catalog.States.Updates;
using LoDb.Ingestion.Pipeline.Versions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Ingestion.Catalog.States;

/// <summary>
/// Registrations of the index of entity states: its self-reloading copy, its writer and the
/// first stage of a promotion.
/// </summary>
/// <remarks>
/// Called by <see cref="CatalogRegistration.AddLoDbCatalog"/>, so that the index's owner fills
/// this file alone. Neither does I/O nor throws: the OpenAPI generation builds the host. The
/// reconciliation job and the <c>catalog states build</c> command are the API's, found by its
/// conventions.
/// </remarks>
public static class StateRegistration
{
    public static IServiceCollection AddLoDbCatalogStates(this IServiceCollection services)
    {
        services.TryAddSingleton<StateIndexMetrics>();
        services.TryAddSingleton<StoredEntityStateIndex>();
        services.TryAddSingleton<StateIndexStamps>();
        services.TryAddSingleton<StateIndexCopy>();
        services.TryAddSingleton<IEntityStateIndex>(
            static provider => provider.GetRequiredService<StateIndexCopy>());
        AddWriter(services);
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IPromotionListener, StateIndexPromotionListener>());
        return services;
    }

    private static void AddWriter(IServiceCollection services)
    {
        services.TryAddSingleton<EnglishEntities>();
        services.TryAddSingleton<CoverageSource>();
        services.TryAddSingleton<GenerationStore>();
        services.TryAddSingleton<VersionWriter>();
        services.TryAddSingleton<GenerationWalker>();
        services.TryAddSingleton<StateIndexLock>();
        services.TryAddSingleton<IStateIndexUpdater, StateIndexUpdater>();
    }
}
