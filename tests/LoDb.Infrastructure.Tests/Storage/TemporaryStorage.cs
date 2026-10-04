using LoDb.Infrastructure.Storage;
using LoDb.Infrastructure.Storage.Blobs;
using LoDb.Infrastructure.Storage.Datasets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests.Storage;

/// <summary>
/// The storage zone as the host registers it, on a fresh temporary root deleted afterwards.
/// </summary>
internal sealed class TemporaryStorage : IDisposable
{
    public const string RootKey = "LoDb:Storage:Root";
    private const string DirectoryPrefix = "lodb-storage-tests-";

    private readonly ServiceProvider _services;

    public TemporaryStorage()
    {
        Root = Directory.CreateTempSubdirectory(DirectoryPrefix).FullName;
        _services = Build(Root);
    }

    public string Root { get; }

    public IBlobStore Blobs => _services.GetRequiredService<IBlobStore>();

    public IDatasetStore Datasets => _services.GetRequiredService<IDatasetStore>();

    /// <summary>A service provider holding only the storage zone, with the given root.</summary>
    public static ServiceProvider Build(string? root)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [RootKey] = root })
            .Build();
        return new ServiceCollection().AddLoDbStorage(configuration).BuildServiceProvider();
    }

    /// <summary>File system path of a relative storage path.</summary>
    public string PathOf(string relativePath) => Path.Combine(Root, relativePath);

    /// <summary>Files left in the staging area.</summary>
    public IReadOnlyList<string> StagingFiles()
    {
        var staging = PathOf(StorageLayout.StagingDirectory);
        return Directory.Exists(staging) ? Directory.GetFiles(staging) : [];
    }

    public void Dispose()
    {
        _services.Dispose();
        Directory.Delete(Root, recursive: true);
    }
}
