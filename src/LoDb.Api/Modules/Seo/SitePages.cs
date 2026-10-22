using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Seo;

/// <summary>
/// The page URLs the crawler-facing files publish, in the grammar of ADR 0005:
/// <c>/{locale}/[{version}/]{path}</c>.
/// </summary>
/// <remarks>
/// Entity paths are never written here: they are the catalog's
/// <see cref="CanonicalPath"/>, the value the API returns and the front links to.
/// </remarks>
internal static class SitePages
{
    /// <summary>Path of the home page below the locale: <c>/{locale}/</c>.</summary>
    public const string Home = "";

    /// <summary>
    /// Path of the patch history below the locale: the page linking every listed version's
    /// page of changes and lists, how crawlers reach every patch by links, those the sitemap
    /// index does not announce included (outside the index of states, or with nothing indexed).
    /// </summary>
    public const string PatchHistory = "patches";

    /// <summary>
    /// Path of a patch's page of changes below the locale and the version prefix:
    /// <c>/{locale}/changes</c> for the latest patch, <c>/{locale}/{version}/changes</c> for
    /// another. Not in <see cref="PatchBound"/>: a patch that changed nothing has a
    /// <c>noindex</c> page, which no sitemap nor IndexNow announces.
    /// </summary>
    public const string Changes = "changes";

    /// <summary>
    /// Pages that exist once per locale, never per version, and change with every promoted
    /// patch: the home, the four lists and the patch history.
    /// </summary>
    public static IReadOnlyList<string> PatchBound => [Home, .. Lists, PatchHistory];

    /// <summary>
    /// Pages that exist once per locale, never per version, and no patch changes: the
    /// editorial pages, in the order the primary sitemap lists them.
    /// </summary>
    public static IReadOnlyList<string> Editorial { get; } =
    [
        "about",
        "about/data",
        "faq",
        "legal/notice",
        "legal/privacy",
        "legal/terms",
        "legal/cookies",
        "donate",
        "developers",
        "trends",
        "changelog",
    ];

    /// <summary>The four list pages, which also exist per version.</summary>
    public static IReadOnlyList<string> Lists =>
        [.. Enum.GetValues<ResourceType>().Select(CanonicalPath.SegmentOf)];

    /// <summary>
    /// Absolute URL every page path of a locale and version follows, ending with a slash:
    /// <paramref name="version"/> is null for the latest one, whose URLs carry no version.
    /// </summary>
    public static string Prefix(string origin, UiLocale locale, PatchVersion? version)
    {
        ArgumentNullException.ThrowIfNull(origin);
        return version is null
            ? $"{origin}/{UiLocales.Code(locale)}/"
            : $"{origin}/{UiLocales.Code(locale)}/{version.Value}/";
    }
}
