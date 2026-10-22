using LoDb.Api.Modules.Seo.Pages;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>
/// What each sitemap lists, as absolute URLs: the index, a locale's primary sitemap and a
/// locale's sitemap of one past version. Only indexable pages, each dated by the moment it
/// last changed when that moment is known.
/// </summary>
/// <remarks>
/// Entity pages come from canonical paths, never rebuilt here: the latest catalog's for the
/// short URLs, the index of states' for a past version, the path its opener version gave the
/// entity. A past version's sitemap reads the index alone, so that announcing every patch
/// costs no catalog load.
/// </remarks>
internal static class SitemapLocations
{
    /// <summary>
    /// Every locale's primary sitemap, dated by the latest promotion, then, version by version
    /// from the newest, every locale's sitemap of each covered version older than the latest
    /// one that lists something, dated by its newest page.
    /// </summary>
    /// <remarks>
    /// An empty version file is still served, but not announced: a patch that changed nothing
    /// and whose states are all open has no page of its own to index. Before any promotion,
    /// no version is a past one.
    /// </remarks>
    public static IEnumerable<SitemapEntry> Index(
        string origin,
        LatestPatch latest,
        EntityStateSnapshot index)
    {
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(index);
        var primary = UiLocales.All.Select(locale => new SitemapEntry(
            origin + SitemapFile.PathOf(locale, null),
            latest.PromotedAt));
        var past = PastVersions(latest.Version, index)
            .Select(version => (Version: version, Modified: index.VersionFileModifiedAt(version)))
            .Where(static file => file.Modified is not null)
            .SelectMany(file => UiLocales.All.Select(locale => new SitemapEntry(
                origin + SitemapFile.PathOf(locale, file.Version),
                file.Modified)));
        return primary.Concat(past);
    }

    /// <summary>
    /// The pages of the latest version, under their short URLs below
    /// <paramref name="prefix"/> (<see cref="SitePages.Prefix"/> without a version): the
    /// pages a patch changes, its page of changes when it changed something, and every entity,
    /// dated by its promotion, then the editorial pages, undated. <paramref name="catalog"/>
    /// is null before the first promotion.
    /// </summary>
    public static IEnumerable<SitemapEntry> Latest(
        string prefix,
        CatalogSnapshot? catalog,
        LatestPatch latest)
    {
        ArgumentNullException.ThrowIfNull(latest);
        IEnumerable<string> changes = latest.HasChanges ? [SitePages.Changes] : [];
        var dated = SitePages.PatchBound.Concat(changes).Concat(EntityPaths(catalog))
            .Select(path => new SitemapEntry(prefix + path, latest.PromotedAt));
        return dated.Concat(SitePages.Editorial.Select(path => new SitemapEntry(prefix + path)));
    }

    /// <summary>
    /// The short URLs below <paramref name="prefix"/> of the pages every patch changes: the
    /// home, the four lists, the patch history, then every entity of
    /// <paramref name="catalog"/>, when there is one.
    /// </summary>
    public static IEnumerable<string> PatchBound(string prefix, CatalogSnapshot? catalog) =>
        SitePages.PatchBound.Concat(EntityPaths(catalog)).Select(path => prefix + path);

    /// <summary>
    /// The pages of a covered past version below <paramref name="prefix"/>
    /// (<see cref="SitePages.Prefix"/> with the version): its page of changes when it changed
    /// something, then the openers of the states it opened that are closed since, the pages
    /// frozen as their own canonical. A state still open enters at its closing.
    /// </summary>
    public static IEnumerable<SitemapEntry> Version(
        string prefix,
        PatchVersion version,
        EntityStateSnapshot index)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(index);
        IEnumerable<SitemapEntry> patchPage = index.HasChanges(version)
            ? [new SitemapEntry(prefix + SitePages.Changes, index.PatchPageModifiedAt(version))]
            : [];
        var openers = index.ClosedOpenedAt(version)
            .Select(state => new SitemapEntry(prefix + state.Path, state.ClosedAt));
        return patchPage.Concat(openers);
    }

    // The covered versions older than the latest one, newest first.
    private static IEnumerable<PatchVersion> PastVersions(
        PatchVersion? latest,
        EntityStateSnapshot index) =>
        latest is null
            ? []
            : index.Coverage.All.Where(version => version < latest).Reverse();

    private static IEnumerable<string> EntityPaths(CatalogSnapshot? catalog) =>
        catalog is null
            ? []
            : VersionPages.ListedEntityPaths(catalog).Select(static path => path.Value);
}
