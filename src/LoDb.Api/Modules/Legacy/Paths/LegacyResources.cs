using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using LoDb.Domain.Catalog;

namespace LoDb.Api.Modules.Legacy.Paths;

/// <summary>
/// The old resource segments: plural for a list, singular before a name; items were
/// "objects".
/// </summary>
/// <remarks>Symfony matched them case-sensitively: so do these tables.</remarks>
internal static class LegacyResources
{
    private static readonly FrozenDictionary<string, ResourceType> Lists =
        new Dictionary<string, ResourceType>
        {
            ["champions"] = ResourceType.Champions,
            ["objects"] = ResourceType.Items,
            ["runes"] = ResourceType.Runes,
            ["summoners"] = ResourceType.Summoners,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, ResourceType> Details =
        new Dictionary<string, ResourceType>
        {
            ["champion"] = ResourceType.Champions,
            ["object"] = ResourceType.Items,
            ["rune"] = ResourceType.Runes,
            ["summoner"] = ResourceType.Summoners,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool TryList(string segment, [NotNullWhen(true)] out ResourceType? resource) =>
        TryFind(Lists, segment, out resource);

    public static bool TryDetail(string segment, [NotNullWhen(true)] out ResourceType? resource) =>
        TryFind(Details, segment, out resource);

    private static bool TryFind(
        FrozenDictionary<string, ResourceType> table,
        string segment,
        [NotNullWhen(true)] out ResourceType? resource)
    {
        resource = table.TryGetValue(segment, out var found) ? found : null;
        return resource is not null;
    }
}
