using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;

namespace LoDb.Api.Modules.Catalog.Search;

/// <summary>Reads the <c>types</c> filter of a search, named as entity paths name them.</summary>
internal static class SearchTypes
{
    private static readonly FrozenDictionary<string, ResourceType> BySegment =
        Enum.GetValues<ResourceType>().ToFrozenDictionary(
            CanonicalPath.SegmentOf,
            static type => type,
            StringComparer.Ordinal);

    /// <summary>The segments a client may name, in the order hits come back.</summary>
    public static string Allowed { get; } = string.Join(
        ", ",
        Enum.GetValues<ResourceType>().Select(CanonicalPath.SegmentOf));

    /// <summary>The resources named; empty, meaning all of them, when none is.</summary>
    public static bool TryParse(
        string[]? values,
        [NotNullWhen(true)] out IReadOnlyCollection<ResourceType>? types)
    {
        HashSet<ResourceType> named = [];
        var tokens = (values ?? [])
            .SelectMany(static value => value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        foreach (var token in tokens)
        {
            if (!BySegment.TryGetValue(token, out var type))
            {
                types = null;
                return false;
            }

            named.Add(type);
        }

        types = named;
        return true;
    }
}
