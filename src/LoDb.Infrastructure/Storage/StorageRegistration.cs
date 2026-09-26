using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Storage;

/// <summary>
/// Registrations of the storage zone: content-addressed blobs and immutable datasets.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class StorageRegistration
{
    public static IServiceCollection AddLoDbStorage(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
