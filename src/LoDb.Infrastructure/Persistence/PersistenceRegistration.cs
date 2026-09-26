using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Persistence;

/// <summary>
/// Registrations of the persistence zone: the database context and its data source.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class PersistenceRegistration
{
    public static IServiceCollection AddLoDbPersistence(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
