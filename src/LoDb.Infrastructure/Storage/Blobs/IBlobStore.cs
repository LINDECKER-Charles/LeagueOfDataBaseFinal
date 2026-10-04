namespace LoDb.Infrastructure.Storage.Blobs;

/// <summary>
/// Content-addressed image store: <c>blobs/{sha256}.{ext}</c> and its WebP sibling
/// <c>blobs/{sha256}.webp</c>, served by nginx under <c>/cdn/blobs/</c>.
/// </summary>
/// <remarks>
/// Every write is atomic and write-once: readers never see a partial file, an existing
/// file is never rewritten, and concurrent writers of the same bytes all succeed. The v1
/// implementation writes to the <c>storage</c> volume; an object store can replace it once
/// there are several hosts (ADR 0004). Transcoding to WebP is the caller's job.
/// </remarks>
public interface IBlobStore
{
    /// <summary>Stores the bytes once under their SHA-256.</summary>
    /// <param name="content">The image bytes.</param>
    /// <param name="extension">
    /// Extension of the source, in any case, with or without a leading dot (<c>.PNG</c>,
    /// <c>png</c>); letters and digits only.
    /// </param>
    /// <param name="cancellationToken">Cancels the write; nothing is then published.</param>
    /// <exception cref="ArgumentException">The extension is invalid.</exception>
    Task<StoredBlob> StoreAsync(
        ReadOnlyMemory<byte> content,
        string extension,
        CancellationToken cancellationToken);

    /// <summary>Whether the blob is stored.</summary>
    Task<bool> ExistsAsync(BlobKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the WebP sibling is stored; always <c>false</c> for a blob without a WebP slot.
    /// </summary>
    /// <remarks>Lets the caller skip a costly transcode.</remarks>
    Task<bool> HasWebpSiblingAsync(BlobKey key, CancellationToken cancellationToken);

    /// <summary>Stores the WebP sibling of a blob, once.</summary>
    /// <param name="key">The blob the sibling belongs to.</param>
    /// <param name="webp">The transcoded bytes.</param>
    /// <param name="cancellationToken">Cancels the write; nothing is then published.</param>
    /// <returns><c>true</c> if this call wrote the sibling, <c>false</c> if it existed.</returns>
    /// <exception cref="ArgumentException">
    /// The blob has no WebP slot (<see cref="BlobKey.HasWebpSlot"/>).
    /// </exception>
    Task<bool> StoreWebpSiblingAsync(
        BlobKey key,
        ReadOnlyMemory<byte> webp,
        CancellationToken cancellationToken);
}
