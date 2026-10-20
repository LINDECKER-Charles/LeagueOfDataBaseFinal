namespace LoDb.Infrastructure.PageStore;

/// <summary>
/// The object store of the pre-rendered catalogue (ADR 0011), on the write side: the API
/// writes pages and build files there, nginx reads them anonymously.
/// </summary>
/// <remarks>
/// <para>
/// Path-addressed (<c>pages/{epoch}/…</c>, <c>build/…</c>), unlike <c>IBlobStore</c>, which
/// is addressed by content and local. Reached only through the <c>page-store</c> client and
/// its allow-list.
/// </para>
/// <para>
/// Three kinds of failure: a <see cref="Errors.PageStoreTransientException"/> (5xx, 429, 408,
/// timeout, transport) is retried later; a <see cref="Errors.PageStoreConfigurationException"/>
/// (401, 403 or 404 on a write, a redirect, 501, a destination off the allow-list) pauses the
/// pass; a <see cref="Errors.PageStoreRefusedException"/> (this object cannot be stored) fails
/// the page alone. A missing object is a value, never an exception.
/// </para>
/// </remarks>
public interface IPageStore
{
    /// <summary>
    /// Writes <paramref name="page"/> in one atomic PUT. A create-only write
    /// (<c>If-None-Match: *</c>) is the way to learn whether a key is taken.
    /// </summary>
    Task<PageWriteOutcome> PutAsync(PageObject page, CancellationToken cancellationToken);

    /// <summary>
    /// What the store keeps under <paramref name="key"/>, or null when it has nothing there or
    /// refuses to say (403 and 404 are a definitive absence).
    /// </summary>
    /// <remarks>
    /// Meaningful only with a key that may read: tests and development. The production key
    /// writes and never reads, so every signed HEAD answers 403 and this returns null whether
    /// or not the object exists. The pass never relies on it: it learns that a key is taken
    /// from a create-only <see cref="PutAsync"/> (412), or from an anonymous read.
    /// </remarks>
    Task<StoredPage?> HeadAsync(string key, CancellationToken cancellationToken);
}
