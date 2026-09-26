namespace LoDb.Infrastructure.Storage.Datasets;

/// <summary>
/// Immutable normalized datasets under <c>data/{version}/{lang}/{type}.json</c>, never
/// exposed on the web.
/// </summary>
/// <remarks>
/// The store deals in UTF-8 bytes and knows no dataset type. A dataset is written once,
/// atomically: readers see nothing or the whole file, and a second write of the same key
/// leaves the first one in place.
/// </remarks>
public interface IDatasetStore
{
    /// <summary>Writes the dataset unless it already exists.</summary>
    /// <param name="key">Where the dataset goes.</param>
    /// <param name="json">The serialized dataset.</param>
    /// <param name="cancellationToken">Cancels the write; nothing is then published.</param>
    /// <returns><c>true</c> if this call wrote it, <c>false</c> if it already existed.</returns>
    Task<bool> WriteAsync(
        DatasetKey key,
        ReadOnlyMemory<byte> json,
        CancellationToken cancellationToken);

    /// <summary>Whether the dataset is stored.</summary>
    Task<bool> ExistsAsync(DatasetKey key, CancellationToken cancellationToken);

    /// <summary>Opens the dataset for an asynchronous read.</summary>
    /// <returns>The stream, owned by the caller, or <c>null</c> if the dataset is absent.</returns>
    Task<Stream?> OpenReadAsync(DatasetKey key, CancellationToken cancellationToken);
}
