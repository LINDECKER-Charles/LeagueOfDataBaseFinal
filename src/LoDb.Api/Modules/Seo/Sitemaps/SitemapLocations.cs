using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>
/// What each sitemap lists, as absolute URLs: the index, a locale's primary sitemap and a
/// locale's sitemap of one past version. Only what the latest patch changed is dated, by its
/// promotion.
/// </summary>
/// <remarks>
/// Entity pages come from the catalog's canonical paths, the ones every list and detail of
/// the API returns: the sitemaps cannot drift from the links of the site.
/// </remarks>
internal static class SitemapLocations
{
    /// <summary>
    /// Every locale's primary sitemap, then, version by version from the newest, every
    /// locale's sitemap of the first <paramref name="window"/> versions listed before the
    /// latest one: it is the primary sitemaps.
    /// </summary>
    /// <remarks>
    /// The window bounds what the index pushes at crawlers, not what the site serves: an
    /// older version's sitemap still answers at its URL, and the patch history links every
    /// listed version. A version listed but not promoted yet stays out: its pages move to
    /// the short URLs at its promotion, and the index would announce redirects. Before any
    /// promotion, the first versions listed fill the window.
    /// </remarks>
    public static IEnumerable<SitemapEntry> Index(
        string origin,
        CatalogVersions versions,
        int window)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var latest = versions.Latest;
        var primary = UiLocales.All.Select(locale => new SitemapEntry(
            origin + SitemapFile.PathOf(locale, null),
            versions.LatestPromotedAt));
        var past = versions.Listed
            .Where(version => latest is null || version < latest)
            .Take(window)
            .SelectMany(version => UiLocales.All.Select(locale =>
                new SitemapEntry(origin + SitemapFile.PathOf(locale, version))));
        return primary.Concat(past);
    }

    /// <summary>
    /// The pages of the latest version, under their short URLs below
    /// <paramref name="prefix"/> (<see cref="SitePages.Prefix"/> without a version): the
    /// pages a patch changes, every entity included, dated by <paramref name="promotedAt"/>,
    /// then the editorial pages, undated. <paramref name="catalog"/> is null before the first
    /// promotion.
    /// </summary>
    public static IEnumerable<SitemapEntry> Latest(
        string prefix,
        CatalogSnapshot? catalog,
        DateTimeOffset? promotedAt)
    {
        var changed = PatchBound(prefix, catalog)
            .Select(url => new SitemapEntry(url, promotedAt));
        return changed.Concat(SitePages.Editorial.Select(path => new SitemapEntry(prefix + path)));
    }

    /// <summary>
    /// The short URLs below <paramref name="prefix"/> of the pages a patch changes: the
    /// home, the four lists, the patch history, then every entity of
    /// <paramref name="catalog"/>, when there is one.
    /// </summary>
    public static IEnumerable<string> PatchBound(string prefix, CatalogSnapshot? catalog)
    {
        IEnumerable<string> entities = catalog is null ? [] : EntityPaths(catalog);
        return SitePages.PatchBound.Concat(entities).Select(path => prefix + path);
    }

    /// <summary>
    /// The pages of a past version: the four lists, then every entity, undated: the
    /// ingestion dates no past version.
    /// </summary>
    public static IEnumerable<SitemapEntry> Version(
        string origin,
        UiLocale locale,
        CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var prefix = SitePages.Prefix(origin, locale, catalog.Version);
        return SitePages.Lists.Concat(EntityPaths(catalog))
            .Select(path => new SitemapEntry(prefix + path));
    }

    // The entries the lists show: debris has no page (UP 10); the Classic twins do.
    private static IEnumerable<string> EntityPaths(CatalogSnapshot catalog) =>
        catalog.Champions.Entries.Select(catalog.PathOf)
            .Concat(catalog.ListedItems.Select(catalog.PathOf))
            .Concat(catalog.Runes.Entries.Select(catalog.PathOf))
            .Concat(catalog.Summoners.Entries.Select(catalog.PathOf))
            .Select(static path => path.Value)
            .Distinct(StringComparer.Ordinal);
}
