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
}
