using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Ingestion.Catalog.States;

/// <summary>
/// Registrations of the index of entity states: its reader, and what builds and extends it.
/// </summary>
/// <remarks>
/// Called by <see cref="CatalogRegistration.AddLoDbCatalog"/>, so that the index's owner fills
/// this file alone. Neither does I/O nor throws: the OpenAPI generation builds the host.
/// </remarks>
public static class StateRegistration
{
    public static IServiceCollection AddLoDbCatalogStates(this IServiceCollection services)
    {
        services.TryAddSingleton<IEntityStateIndex, StoredEntityStateIndex>();
        return services;
    }
}
