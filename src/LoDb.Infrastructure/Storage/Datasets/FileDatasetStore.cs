namespace LoDb.Infrastructure.Storage.Datasets;

/// <summary>
/// <see cref="IDatasetStore"/> on the local storage volume.
/// </summary>
internal sealed class FileDatasetStore(StorageRoot root, AtomicFileWriter writer)
    : IDatasetStore
{
    public Task<bool> WriteAsync(
        DatasetKey key,
        ReadOnlyMemory<byte> json,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return writer.PublishAsync(root.Resolve(key.RelativePath), json, cancellationToken);
    }

    public Task<bool> ExistsAsync(DatasetKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Task.FromResult(File.Exists(root.Resolve(key.RelativePath)));
    }

    public Task<Stream?> OpenReadAsync(DatasetKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var path = root.Resolve(key.RelativePath);
        try
        {
            return Task.FromResult<Stream?>(new FileStream(path, ReadOptions()));
        }
        catch (Exception exception)
            when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            // Absence is a value: the caller fetches or ingests the dataset.
            return Task.FromResult<Stream?>(null);
        }
    }

    // A fresh instance per read: FileStreamOptions is mutable, so it is never shared.
    private static FileStreamOptions ReadOptions() => new()
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.Read,
        Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
    };
}
