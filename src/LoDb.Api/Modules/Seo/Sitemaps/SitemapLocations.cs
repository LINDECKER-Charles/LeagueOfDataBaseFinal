using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>
/// What each sitemap lists, as absolute URLs: the index, a locale's primary sitemap and a
/// locale's sitemap of one past version.
/// </summary>
/// <remarks>
/// Entity pages come from the catalog's canonical paths, the ones every list and detail of
/// the API returns: the sitemaps cannot drift from the links of the site.
/// </remarks>
internal static class SitemapLocations
{
    /// <summary>
    /// Every locale's primary sitemap, then, version by version from the newest, every
    /// locale's sitemap of the first <paramref name="window"/> versions listed, the latest
    /// one aside: it is the primary sitemaps.
    /// </summary>
    /// <remarks>
    /// The window bounds what the index pushes at crawlers, not what the site serves: an
    /// older version's sitemap still answers at its URL, and the patch history links every
    /// listed version.
    /// </remarks>
    public static IEnumerable<string> Index(
        string origin,
        CatalogVersions versions,
        int window)
    {
        ArgumentNullException.ThrowIfNull(versions);
        IEnumerable<PatchVersion?> files =
        [
            null,
            .. versions.Listed.Where(version => version != versions.Latest).Take(window),
        ];
        return files.SelectMany(version => UiLocales.All.Select(locale =>
            origin + SitemapFile.PathOf(locale, version)));
    }

    /// <summary>
    /// The pages of the latest version, under their short URLs: the unversioned pages, then
    /// every entity. <paramref name="catalog"/> is null before the first promotion.
    /// </summary>
    public static IEnumerable<string> Latest(
        string origin,
        UiLocale locale,
        CatalogSnapshot? catalog)
    {
        IEnumerable<string> entities = catalog is null ? [] : EntityPaths(catalog);
        var prefix = SitePages.Prefix(origin, locale, null);
        return SitePages.Unversioned.Concat(entities).Select(path => prefix + path);
    }

    /// <summary>The pages of a past version: the four lists, then every entity.</summary>
    public static IEnumerable<string> Version(
        string origin,
        UiLocale locale,
        CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var prefix = SitePages.Prefix(origin, locale, catalog.Version);
        return SitePages.Lists.Concat(EntityPaths(catalog)).Select(path => prefix + path);
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
