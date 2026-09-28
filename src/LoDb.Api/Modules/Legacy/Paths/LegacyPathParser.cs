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

    /// <param name="path">The path, with or without its leading slash.</param>
    /// <returns>The old URL read, or <see langword="null"/> when no old route had it.</returns>
    public static LegacyPath? Parse(string? path)
    {
        var segments = SegmentsOf(path);
        if (segments.Length == 0)
        {
            return null;
        }

        return LegacyPages.Find(segments) ?? ParseCatalog(segments);
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

    // VersionManager::VERSION_PATTERN, the old requirement of every {version} segment.
    [GeneratedRegex(@"^[0-9]+(?:\.[0-9]+)+\z")]
    private static partial Regex VersionSegment();
}
