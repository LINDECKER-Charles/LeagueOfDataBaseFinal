using System.Collections.Frozen;
using LoDb.Domain.Catalog.Champions;

namespace LoDb.Ingestion.Catalog.Snapshots;

/// <summary>
/// The loose forms a visitor types for a champion: its id or its English name, in any case,
/// without spaces nor punctuation ("aatrox", "wukong", "nunu-willump", "kai'sa").
/// </summary>
/// <remarks>
/// The English name, whatever the catalog's language: an address is typed in the Latin
/// alphabet, as the ids are. Ids win over names, and of two champions folding to the same
/// form, the first listed.
/// </remarks>
internal static class ChampionAliases
{
    /// <summary>Folded form → champion id, for every champion of the catalog.</summary>
    public static FrozenDictionary<string, string> Of(
        IReadOnlyList<ChampionDetail> champions,
        CatalogResource<ChampionDetail> english)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in champions.Select(static champion => champion.Summary.Id))
        {
            aliases.TryAdd(Fold(id), id);
        }

        foreach (var id in champions.Select(static champion => champion.Summary.Id))
        {
            if (english.Find(id)?.Summary.Name is { } name)
            {
                aliases.TryAdd(Fold(name), id);
            }
        }

        return aliases.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// ASCII letters and digits, lowercased: "Nunu &amp; Willump" → "nunuwillump".
    /// </summary>
    public static string Fold(string text) =>
        string.Concat(text.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant));
}
