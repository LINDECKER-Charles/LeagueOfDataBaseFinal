using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Queue;

/// <summary>
/// The long tail (ADR 0003): stores on demand what the proactive ingestion did not.
/// </summary>
/// <remarks>
/// <para>
/// Synchronous mode for a detail page, the picker, a build or the search: the caller waits
/// for what it needs. Queued mode for lists and previews: the caller answers with
/// placeholders and a <c>Retry-After</c>, and the background worker ingests.
/// </para>
/// <para>
/// Identical work is merged: callers waiting for the same dataset or image share one
/// ingestion, which a caller giving up does not cancel. At most
/// <c>LoDb:Ingestion:SyncConcurrency</c> ingestions run at once on an instance. Crawlers must
/// not use the synchronous mode: their work is queued, within a budget per minute (C5).
/// </para>
/// </remarks>
public interface IOnDemandIngestion
{
    /// <summary>
    /// Stores the four datasets of a (version, language) unless they are stored.
    /// </summary>
    /// <returns>False when Data Dragon lists neither the version nor the language.</returns>
    /// <exception cref="Egress.Errors.EgressException">
    /// Data Dragon failed: nothing is stored and a later call tries again.
    /// </exception>
    Task<bool> EnsureDatasetsAsync(
        PatchVersion version,
        DdragonLanguage language,
        CancellationToken cancellationToken);

    /// <summary>
    /// Settles the images the manifest has no verdict for: present with a blob, or absent
    /// after a 403/404. A transient failure leaves an image without a verdict, for a later
    /// call; the verdicts are then read from the manifest. A version Data Dragon does not
    /// list is left alone.
    /// </summary>
    Task EnsureImagesAsync(
        PatchVersion version,
        IReadOnlyCollection<DdragonImage> images,
        CancellationToken cancellationToken);

    /// <summary>Queues work for the background worker, merged with the same work waiting.</summary>
    /// <returns>
    /// True when the work is queued or already waiting; false when the queue is full or the
    /// crawler budget is spent, and nothing is queued.
    /// </returns>
    bool TryEnqueue(OnDemandRequest request);
}
