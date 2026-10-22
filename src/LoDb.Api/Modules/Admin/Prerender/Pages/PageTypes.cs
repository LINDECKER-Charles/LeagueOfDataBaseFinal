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
    /// The paths of <paramref name="paths"/> of one of <paramref name="types"/>, each list then
    /// its entities, in their order; all of them when <paramref name="types"/> is null.
    /// </summary>
    /// <remarks>
    /// The patch page (<c>changes</c>) is of no type: any filter leaves it out, so that only a
    /// unit over every type renders it, and the four bits of <see cref="TypeMask"/> still say
    /// what an epoch holds.
    /// </remarks>
    public static IReadOnlyList<string> Keep(
        IReadOnlyList<string> paths,
        IReadOnlyList<ResourceType>? types)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (types is null)
        {
            return paths;
        }

        var segments = types.Select(CanonicalPath.SegmentOf).ToList();
        return [.. paths.Where(path => segments.Exists(segment => IsOf(path, segment)))];
    }

    private static bool IsOf(string path, string segment) =>
        path.StartsWith(segment, StringComparison.Ordinal)
        && (path.Length == segment.Length || path[segment.Length] == Separator);
}
