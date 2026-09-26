namespace LoDb.Infrastructure.Storage.Blobs;

/// <summary>
/// <see cref="IBlobStore"/> on the local storage volume.
/// </summary>
internal sealed class FileBlobStore(StorageRoot root, AtomicFileWriter writer) : IBlobStore
{
    public async Task<StoredBlob> StoreAsync(
        ReadOnlyMemory<byte> content,
        string extension,
        CancellationToken cancellationToken)
    {
        var key = BlobKey.ForContent(content.Span, extension);
        var written = await writer.PublishAsync(
            root.Resolve(key.RelativePath),
            content,
            cancellationToken);
        return new StoredBlob(key, written);
    }

    public Task<bool> ExistsAsync(BlobKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Task.FromResult(File.Exists(root.Resolve(key.RelativePath)));
    }

    public Task<bool> HasWebpSiblingAsync(BlobKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var sibling = key.WebpRelativePath;
        return Task.FromResult(sibling is not null && File.Exists(root.Resolve(sibling)));
    }

    public Task<bool> StoreWebpSiblingAsync(
        BlobKey key,
        ReadOnlyMemory<byte> webp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var sibling = key.WebpRelativePath
            ?? throw new ArgumentException(
                $"The blob '{key.FileName}' has no WebP sibling.",
                nameof(key));
        return writer.PublishAsync(root.Resolve(sibling), webp, cancellationToken);
    }
}
