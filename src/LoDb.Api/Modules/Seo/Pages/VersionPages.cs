using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Seo.Pages;

/// <summary>
/// The pages the site serves for one (version, locale), as paths below the
/// <c>/{locale}/{version}/</c> prefix: the four lists, the patch page, then every entity the
/// lists show.
/// </summary>
/// <remarks>
/// <para>
/// One source for the pre-render pass and its queues, and of the entities for the sitemaps
/// (<see cref="ListedEntityPaths"/>), so that none drifts from the others. Unlike a past
/// version's sitemap, which keeps only the pages that are their own canonical, this is every
/// page the site may answer for that version. The home and the patch history exist once per
/// locale, never per version, and are not listed.
/// </para>
/// <para>
/// The order is frozen: a unit of the pass keeps its place as an index in this list (its
/// cursor), so a change of it under a running pass would skip or repeat pages. The patch page
/// stands after the lists whether the version has one or not (the oldest covered version, or
/// one outside the index, answers a 404 the pass skips): the list never depends on the index.
/// </para>
/// </remarks>
internal static class VersionPages
{
    /// <summary>
    /// The patch page below <c>/{locale}/{version}/</c>: what the version changed. A page of no
    /// type: only a pass over every type renders it.
    /// </summary>
    public const string PatchChanges = "changes";

    /// <summary>
    /// The canonical path of every entity the lists of <paramref name="catalog"/> show, once
    /// each: debris and placeholder spells have no page (UP 10); the Classic twins do. The
    /// index of states holds the same entities (<see cref="ListedEntities"/>).
    /// </summary>
    public static IEnumerable<CanonicalPath> ListedEntityPaths(CatalogSnapshot catalog) =>
        ListedEntities.PathsOf(catalog);

    /// <summary>
    /// The four lists, the patch page, then the listed entities of <paramref name="catalog"/>.
    /// </summary>
    public static IEnumerable<string> Of(CatalogSnapshot catalog) =>
        SitePages.Lists
            .Append(PatchChanges)
            .Concat(ListedEntityPaths(catalog).Select(static path => path.Value));
}
