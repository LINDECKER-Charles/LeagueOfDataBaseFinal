using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// The type of a page, named by the first segment of its path (<c>champions</c>,
/// <c>items/1036-long-sword</c>): what a scope restricted to one type keeps.
/// </summary>
internal static class PageTypes
{
    private const char Separator = '/';

    /// <summary>
    /// The type a path segment names, as <see cref="CanonicalPath.SegmentOf"/> writes it.
    /// </summary>
    public static bool TryParse(string? segment, out ResourceType type)
    {
        foreach (var candidate in Enum.GetValues<ResourceType>())
        {
            var name = CanonicalPath.SegmentOf(candidate);
            if (string.Equals(name, segment, StringComparison.Ordinal))
            {
                type = candidate;
                return true;
            }
        }

        type = default;
        return false;
    }

    /// <summary>
    /// The paths of <paramref name="paths"/> of type <paramref name="type"/>, the list then its
    /// entities, in their order; all of them when <paramref name="type"/> is null.
    /// </summary>
    public static IReadOnlyList<string> Keep(IReadOnlyList<string> paths, ResourceType? type)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (type is not { } kept)
        {
            return paths;
        }

        var segment = CanonicalPath.SegmentOf(kept);
        return [.. paths.Where(path => IsOf(path, segment))];
    }

    private static bool IsOf(string path, string segment) =>
        path.StartsWith(segment, StringComparison.Ordinal)
        && (path.Length == segment.Length || path[segment.Length] == Separator);
}
