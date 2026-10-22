using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Seo.Pages;

/// <summary>
/// The pages the site serves for one (version, locale), as paths below the
/// <c>/{locale}/{version}/</c> prefix: the four lists, then every entity the lists show.
/// </summary>
/// <remarks>
/// One source for the sitemaps and for the pre-render pass, so neither can drift from the
/// other. Unlike a past version's sitemap, which keeps only the pages that are still their own
/// canonical, this is every page the site answers for that version. The home and the patch
/// history exist once per locale, never per version, and are not listed.
/// </remarks>
internal static class VersionPages
{
    /// <summary>
    /// The canonical path of every entity the lists of <paramref name="catalog"/> show, once
    /// each: debris and placeholder spells have no page (UP 10); the Classic twins do. The
    /// index of states holds the same entities (<see cref="ListedEntities"/>).
    /// </summary>
    public static IEnumerable<CanonicalPath> ListedEntityPaths(CatalogSnapshot catalog) =>
        ListedEntities.PathsOf(catalog);

    /// <summary>The four lists, then the listed entities of <paramref name="catalog"/>.</summary>
    public static IEnumerable<string> Of(CatalogSnapshot catalog) =>
        SitePages.Lists.Concat(ListedEntityPaths(catalog).Select(static path => path.Value));
}
