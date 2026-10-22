using LoDb.Api.Modules.Seo.Sitemaps;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Seo.IndexNow;

/// <summary>
/// What the promotion of a patch announces to IndexNow, in every locale, by category and in
/// the order it is sent: the short URLs first, which a refusal must not drop, then the pages
/// of changes, then the openers of the states closed since the previous latest patch.
/// </summary>
/// <remarks>
/// The short URLs are every page a patch touches, as <c>latest.xml</c> dates them, changed or
/// not. What changed comes from the index of states, never from comparing catalogs, so that a
/// reconciled index announces the same pages as the promotion would have.
/// </remarks>
/// <param name="ShortUrls">The pages of the latest patch, under their short URLs.</param>
/// <param name="PatchPages">
/// <c>/{locale}/changes</c> and <c>/{locale}/{version}/changes</c> of each version the
/// promotion made a past one, each when its patch changed something.
/// </param>
/// <param name="Openers">
/// The openers of the states closed since the previous latest patch, frozen as their own
/// canonical.
/// </param>
internal sealed record IndexNowPlan(
    IReadOnlyList<string> ShortUrls,
    IReadOnlyList<string> PatchPages,
    IReadOnlyList<string> Openers)
{
    /// <summary>Every URL, short ones first.</summary>
    public IEnumerable<string> Urls => ShortUrls.Concat(PatchPages).Concat(Openers);

    public int UrlCount => ShortUrls.Count + PatchPages.Count + Openers.Count;

    /// <summary>
    /// The requests the URLs take, <see cref="IndexNowClient.MaxUrls"/> at most each.
    /// </summary>
    public int RequestCount => (UrlCount + IndexNowClient.MaxUrls - 1) / IndexNowClient.MaxUrls;

    /// <summary>
    /// The plan of the promotion of <paramref name="catalog"/>'s version, which
    /// <paramref name="index"/> covers, under <paramref name="origin"/>.
    /// </summary>
    public static IndexNowPlan Of(string origin, CatalogSnapshot catalog, PromotionIndex index)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new(
            ShortUrlsOf(origin, catalog),
            PatchPagesOf(origin, catalog.Version, index),
            OpenersOf(origin, catalog.Version, index));
    }

    /// <summary>The short URLs of <paramref name="catalog"/>'s pages, locale by locale.</summary>
    public static IReadOnlyList<string> ShortUrlsOf(string origin, CatalogSnapshot catalog) =>
    [
        .. UiLocales.All.SelectMany(locale => SitemapLocations.PatchBound(
            SitePages.Prefix(origin, locale, null),
            catalog)),
    ];

    /// <summary>
    /// The pages of changes that changed something among those of <paramref name="promoted"/>,
    /// now under the short URL, and of the versions it made past ones, under their versioned
    /// URL: the previous latest patch's, which leaves the short URL, and any listed since.
    /// </summary>
    public static IReadOnlyList<string> PatchPagesOf(
        string origin,
        PatchVersion promoted,
        PromotionIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        // A null version prefixes the short URL, the latest patch's.
        IEnumerable<PatchVersion?> latest = index.States.HasChanges(promoted) ? [null] : [];
        var versions = latest.Concat(index.PastSince(promoted)
            .Where(index.States.HasChanges)
            .Select(static version => (PatchVersion?)version))
            .ToList();
        return
        [
            .. UiLocales.All.SelectMany(locale => versions.Select(version =>
                SitePages.Prefix(origin, locale, version) + SitePages.Changes)),
        ];
    }

    /// <summary>
    /// The openers of the states closed since the previous latest patch, by the promotion of
    /// <paramref name="promoted"/> or by the versions listed before it: those whose last
    /// version is one it made past, each under its opener's versioned URL.
    /// </summary>
    public static IReadOnlyList<string> OpenersOf(
        string origin,
        PatchVersion promoted,
        PromotionIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        if (index.PastSince(promoted) is not [.., var oldest])
        {
            return [];
        }

        var closed = index.States.ClosedBetween(oldest, promoted);
        return
        [
            .. UiLocales.All.SelectMany(locale => closed.Select(state =>
                SitePages.Prefix(origin, locale, state.Opener) + state.Path)),
        ];
    }
}
