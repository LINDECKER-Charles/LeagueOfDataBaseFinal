using LoDb.Api.Modules.Seo.Pages;
using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog.Canonicals;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>
/// What each sitemap lists, as absolute URLs: the index, a locale's primary sitemap and a
/// locale's sitemap of one past version. Only canonical URLs (ADR 0005, amendment of
/// 2026-10-14), and only what the latest patch changed is dated, by its promotion.
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
    /// older version's sitemap still answers at its URL. A version listed but not promoted
    /// yet stays out: its pages move to the short URLs at its promotion, and the index would
    /// announce redirects. Before any promotion, the first versions listed fill the window.
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
        IEnumerable<string> entities = catalog is null
            ? []
            : VersionPages.ListedEntityPaths(catalog).Select(static path => path.Value);
        return SitePages.PatchBound.Concat(entities).Select(path => prefix + path);
    }

    /// <summary>
    /// The pages of a past version, below <paramref name="prefix"/>
    /// (<see cref="SitePages.Prefix"/> with the version) that are still their own canonical:
    /// those of the entities it was the last to hold, undated, the ingestion dating no past
    /// version. Its lists and the other entities are indexed under the latest short URLs.
    /// </summary>
    public static IEnumerable<SitemapEntry> Version(
        string prefix,
        CatalogSnapshot catalog,
        CanonicalReferences references)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(references);
        return VersionPages.ListedEntityPaths(catalog)
            .Where(references.IsOwnCanonical)
            .Select(path => new SitemapEntry(prefix + path.Value));
    }
}
