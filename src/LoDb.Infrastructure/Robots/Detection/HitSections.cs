using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;

namespace LoDb.Infrastructure.Robots.Detection;

/// <summary>
/// What a request asked for, read from its path alone by the URL grammar of ADR 0005: the
/// home page, a list or a detail of the latest patch, a page of a past patch (<c>archive</c>),
/// another page of the site, a sitemap, <c>robots.txt</c>, the API, a file.
/// </summary>
public static class HitSections
{
    public const string Home = "home";
    public const string List = "list";
    public const string Detail = "detail";
    public const string Archive = "archive";
    public const string Page = "page";
    public const string Sitemap = "sitemap";
    public const string Robots = "robots";
    public const string Api = "api";
    public const string Asset = "asset";
    public const string Other = "other";

    private const string RobotsPath = "/robots.txt";
    private const string SitemapWord = "sitemap";
    private const string XmlExtension = ".xml";
    private const char SegmentSeparator = '/';
    private const char ExtensionMark = '.';
    private const int ResourceIndex = 1;
    private const int DetailSegments = 3;

    private static readonly string[] ApiPrefixes = ["/api/", "/v1/"];
    private static readonly string[] AssetPrefixes = ["/cdn/", "/media/", "/fonts/", "/i18n/"];
    private static readonly char[] PathEnds = ['?', '#'];

    private static readonly HashSet<string> Resources = new(
        Enum.GetValues<ResourceType>().Select(CanonicalPath.SegmentOf),
        StringComparer.Ordinal);

    /// <summary>Every section, in the order the reports list them.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Home, List, Detail, Archive, Page, Sitemap, Robots, Api, Asset, Other];

    /// <summary>Whether the section is a page a reader would open: what a pace counts.</summary>
    public static bool IsPage(string section) =>
        section is Home or List or Detail or Archive or Page;

    /// <param name="target">The path and query requested.</param>
    public static string Of(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var end = target.IndexOfAny(PathEnds);
        var path = end < 0 ? target : target[..end];
        if (path is "" or "/")
        {
            return Home;
        }

        return FileSection(path) ?? PrefixSection(path) ?? LocaleSection(path) ?? Other;
    }

    private static string? FileSection(string path)
    {
        if (path == RobotsPath)
        {
            return Robots;
        }

        return path.Contains(SitemapWord, StringComparison.Ordinal)
            && path.EndsWith(XmlExtension, StringComparison.Ordinal)
                ? Sitemap
                : null;
    }

    private static string? PrefixSection(string path)
    {
        if (StartsWithAny(path, ApiPrefixes))
        {
            return Api;
        }

        var last = path[(path.LastIndexOf(SegmentSeparator) + 1)..];
        return StartsWithAny(path, AssetPrefixes)
            || (last.Contains(ExtensionMark, StringComparison.Ordinal) && !IsLocalePath(path))
                ? Asset
                : null;
    }

    private static string? LocaleSection(string path)
    {
        var segments = path.Split(SegmentSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || !UiLocales.TryParse(segments[0], out _))
        {
            return null;
        }

        if (segments.Length == ResourceIndex)
        {
            return Home;
        }

        var second = segments[ResourceIndex];
        if (char.IsAsciiDigit(second[0]))
        {
            return Archive;
        }

        if (!Resources.Contains(second))
        {
            return Page;
        }

        return segments.Length < DetailSegments ? List : Detail;
    }

    private static bool StartsWithAny(string path, string[] prefixes) =>
        Array.Exists(prefixes, prefix => path.StartsWith(prefix, StringComparison.Ordinal));

    private static bool IsLocalePath(string path)
    {
        var second = path.IndexOf(SegmentSeparator, 1);
        var first = second < 0 ? path[1..] : path[1..second];
        return UiLocales.TryParse(first, out _);
    }
}
