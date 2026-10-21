using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>What the pass renders: the older patches, and the pages of each.</summary>
internal interface IPrerenderCatalog
{
    /// <summary>
    /// Every version Data Dragon lists older than the latest one, newest first: the pinned
    /// versions, whose pages the site serves from the store. None before the first promotion.
    /// </summary>
    Task<IReadOnlyList<PatchVersion>> PinnedVersionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The latest version, which the SSR serves and no pass renders; null before the first
    /// promotion.
    /// </summary>
    Task<PatchVersion?> LatestVersionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// From when every server renders <paramref name="version"/> as an older patch: the API
    /// and the SSR each keep the latest version a while, so a version that just left the latest
    /// rank still renders as the latest one for some minutes after its promotion. Null while it
    /// is not an older patch.
    /// </summary>
    Task<DateTimeOffset?> PinnedFromAsync(
        PatchVersion version,
        CancellationToken cancellationToken);

    /// <summary>
    /// The pages of <paramref name="version"/>, the same paths in every locale: the four lists,
    /// then every entity they show.
    /// </summary>
    Task<VersionPageList> PagesAsync(PatchVersion version, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the datasets of <paramref name="version"/> now, waiting for their ingestion as a
    /// command line does. <see cref="PagesAsync"/> only queues it, for the worker of the
    /// instance: a command's host starts none, and its queue would never be read.
    /// </summary>
    /// <exception cref="LoDb.Ingestion.Egress.Errors.EgressException">
    /// Data Dragon failed: nothing is stored.
    /// </exception>
    Task IngestAsync(PatchVersion version, CancellationToken cancellationToken);
}
