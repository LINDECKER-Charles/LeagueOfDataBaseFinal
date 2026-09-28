using LoDb.Infrastructure.Storage.Blobs;
using LoDb.Infrastructure.Storage.Datasets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Infrastructure.Storage;

/// <summary>
/// Registrations of the storage zone: content-addressed blobs and immutable datasets.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host. The
/// root is therefore checked when the host starts, or when a store is first resolved.
/// </remarks>
public static class StorageRegistration
{
    private const string RootMustBeAbsolute =
        "LoDb:Storage:Root must be set to an absolute directory.";

    public static IServiceCollection AddLoDbStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(static options => IsAbsolute(options.Root), RootMustBeAbsolute)
            .ValidateOnStart();
        services.TryAddSingleton<StorageRoot>();
        services.TryAddSingleton<AtomicFileWriter>();
        services.TryAddSingleton<IBlobStore, FileBlobStore>();
        services.TryAddSingleton<IDatasetStore, FileDatasetStore>();
        return services;
    }

    private static bool IsAbsolute(string root) =>
        !string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root);
}
