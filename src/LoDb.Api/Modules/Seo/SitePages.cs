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
    /// Pages that exist once per locale, never per version, in the order the primary
    /// sitemap lists them: the home, the four lists, then the editorial pages.
    /// </summary>
    public static IReadOnlyList<string> Unversioned { get; } =
    [
        Home,
        .. Lists,
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
