using System.Text.RegularExpressions;

namespace LoDb.Api.Modules.Legacy.Paths;

/// <summary>Reads an old URL's path into a <see cref="LegacyPath"/>.</summary>
/// <remarks>
/// A trailing slash is tolerated, as Symfony redirected it; any other shape is unknown. The
/// version segment only needs the old route requirement here: whether Data Dragon lists it
/// is the resolver's question.
/// </remarks>
internal static partial class LegacyPathParser
{
    private const char Separator = '/';

    // The numbers after an older list segment were its page and filters, which are dropped.
    private const int MaxListNumbers = 2;

    /// <param name="path">The path, with or without its leading slash.</param>
    /// <returns>The old URL read, or <see langword="null"/> when no old route had it.</returns>
    public static LegacyPath? Parse(string? path)
    {
        var segments = SegmentsOf(path);
        if (segments.Length == 0)
        {
            return null;
        }

        return LegacyPages.Find(segments) ?? ParseCatalog(segments) ?? ParseRedirect(segments);
    }

    private static string[] SegmentsOf(string? path)
    {
        var trimmed = (path ?? string.Empty).Trim(Separator);
        return trimmed.Length == 0 ? [] : trimmed.Split(Separator);
    }

    private static LegacyPath? ParseCatalog(string[] segments)
    {
        var version = VersionSegment().IsMatch(segments[0]) ? segments[0] : null;
        var rest = version is null ? segments : segments[1..];
        return rest switch
        {
            [var plural] when LegacyResources.TryList(plural, out var resource) =>
                LegacyPath.List(resource.Value, version),
            [var singular, var name] when LegacyResources.TryDetail(singular, out var resource) =>
                LegacyPath.Detail(resource.Value, name, version),
            _ => null,
        };
    }

    // The routes older than the versioned ones: never a version, a detail as the unversioned
    // one, a list followed by up to two numbers.
    private static LegacyPath? ParseRedirect(string[] segments) => segments switch
    {
        [var plural, .. var numbers] when numbers.Length <= MaxListNumbers
            && Array.TrueForAll(numbers, NumberSegment().IsMatch)
            && LegacyResources.TryRedirectList(plural, out var resource) =>
            LegacyPath.List(resource.Value, null),
        [var singular, var name]
            when LegacyResources.TryRedirectDetail(singular, out var resource) =>
            LegacyPath.Detail(resource.Value, name, null),
        _ => null,
    };

    [GeneratedRegex(@"^[0-9]+\z")]
    private static partial Regex NumberSegment();

    // VersionManager::VERSION_PATTERN, the old requirement of every {version} segment.
    [GeneratedRegex(@"^[0-9]+(?:\.[0-9]+)+\z")]
    private static partial Regex VersionSegment();
}
