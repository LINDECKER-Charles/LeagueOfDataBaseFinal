namespace LoDb.Infrastructure.Storage.Blobs;

/// <summary>
/// Outcome of <see cref="IBlobStore.StoreAsync"/>.
/// </summary>
/// <param name="Key">Address of the stored bytes.</param>
/// <param name="Written">
/// <c>true</c> if this call wrote the file; <c>false</c> if the same bytes were already
/// there, including when a concurrent writer published them first.
/// </param>
public sealed record StoredBlob(BlobKey Key, bool Written);
