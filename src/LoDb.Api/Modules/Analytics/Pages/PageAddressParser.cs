using System.Collections.Frozen;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace LoDb.Api.Modules.Analytics.Pages;

/// <summary>
/// Reads the grammar of the counted pages from a requested path and query, the way the
/// front's router cuts them: the home page, a list and a detail of the latest version.
/// </summary>
/// <remarks>
/// Anything else is not counted: a pinned version (<c>/fr/16.1.1/items</c>), the other
/// pages of the site, the files. Segments are kept as requested, still percent-encoded.
/// </remarks>
internal static class PageAddressParser
{
    private const char SegmentSeparator = '/';
    private const char QueryStart = '?';
    private const char FragmentStart = '#';
    private const string LangKey = "lang";
    private const string VersionKey = "version";
    private const int ResourceIndex = 1;
    private const int EntryIndex = 2;
    private const int MaxSegments = 3;

    private static readonly FrozenDictionary<string, ResourceType> Resources = Enum
        .GetValues<ResourceType>()
        .ToFrozenDictionary(CanonicalPath.SegmentOf, static type => type, StringComparer.Ordinal);

    /// <param name="target">The path and query requested, with a fragment at most.</param>
    /// <returns>Null when the address cannot show a counted page.</returns>
    public static PageAddress? Parse(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var fragment = target.IndexOf(FragmentStart, StringComparison.Ordinal);
        var address = fragment < 0 ? target : target[..fragment];
        var queryStart = address.IndexOf(QueryStart, StringComparison.Ordinal);
        var path = queryStart < 0 ? address : address[..queryStart];
        if (SegmentsOf(path) is not { } segments
            || !UiLocales.TryParse(segments[0], out var locale))
        {
            return null;
        }

        ResourceType? resource = null;
        if (segments.Length > ResourceIndex)
        {
            if (!Resources.TryGetValue(segments[ResourceIndex], out var type))
            {
                return null;
            }

            resource = type;
        }

        var query = queryStart < 0 ? string.Empty : address[queryStart..];
        return Build(segments, locale, resource, QueryHelpers.ParseQuery(query));
    }

    private static PageAddress Build(
        string[] segments,
        UiLocale locale,
        ResourceType? resource,
        Dictionary<string, StringValues> query) => new()
    {
        Locale = locale,
        Resource = resource,
        Entry = segments.Length > EntryIndex ? segments[EntryIndex] : null,
        Lang = FirstOf(query, LangKey),
        Version = FirstOf(query, VersionKey),
        Path = PathOf(segments),
    };

    // "/fr/" for the home page, "/fr/items" and "/fr/items/1036-long-sword" otherwise.
    private static string PathOf(string[] segments)
    {
        var separator = SegmentSeparator.ToString();
        var joined = separator + string.Join(separator, segments);
        return segments.Length == 1 ? joined + separator : joined;
    }

    // The segments of an absolute path, one trailing slash allowed; null on an empty one.
    private static string[]? SegmentsOf(string path)
    {
        if (path.Length <= 1 || !path.StartsWith(SegmentSeparator))
        {
            return null;
        }

        var inner = path.EndsWith(SegmentSeparator) ? path[1..^1] : path[1..];
        var segments = inner.Split(SegmentSeparator);
        return segments.Length > MaxSegments || Array.Exists(segments, string.IsNullOrEmpty)
            ? null
            : segments;
    }

    // Trimmed as the legacy stack read its query: a blank value is no value.
    private static string? FirstOf(
        Dictionary<string, StringValues> query,
        string key)
    {
        var value = query.TryGetValue(key, out var values) ? values[0]?.Trim() : null;
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
