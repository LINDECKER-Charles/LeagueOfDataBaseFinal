using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Summoners;

namespace LoDb.Domain.Editions;

/// <summary>
/// Links the items and summoner spells that exist in both games to their twin (UP 6).
/// </summary>
/// <remarks>
/// A twin is confirmed by id and by name. The id alone lies: Riot reused item ids over the
/// years, so 773001 (the classic "Abyssal Scepter") strips to 3001, a different item today.
/// A twin is the namesake the reader may confuse the entry with, or nothing, and a link never
/// points to an id the dataset does not carry.
/// </remarks>
public static class EditionTwins
{
    /// <summary>The same items, each with its confirmed twin or none.</summary>
    public static IReadOnlyList<Item> LinkItems(IReadOnlyList<Item> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var twins = Describe(items, item => new EditionTwin
        {
            Id = item.Id,
            Name = item.Name,
            Edition = item.Edition,
        });

        return [.. items.Select(item => item with
        {
            Counterpart = Confirm(item.Name, ItemEdition.CounterpartId(item.Id), twins),
        })];
    }

    /// <summary>The same spells, each with its confirmed twin or none.</summary>
    public static IReadOnlyList<SummonerSpell> LinkSummonerSpells(
        IReadOnlyList<SummonerSpell> spells)
    {
        ArgumentNullException.ThrowIfNull(spells);
        var twins = Describe(spells, spell => new EditionTwin
        {
            Id = spell.Id,
            Name = spell.Name,
            Edition = spell.Edition,
        });

        return [.. spells.Select(spell => spell with
        {
            Counterpart = Confirm(spell.Name, SummonerSpellEdition.CounterpartId(spell.Id), twins),
        })];
    }

    // The first entry of an id wins, as in the id-keyed dataset map.
    private static Dictionary<string, EditionTwin> Describe<T>(
        IEnumerable<T> entries,
        Func<T, EditionTwin> describe)
    {
        var twins = new Dictionary<string, EditionTwin>(StringComparer.Ordinal);
        foreach (var twin in entries.Select(describe))
        {
            twins.TryAdd(twin.Id, twin);
        }

        return twins;
    }

    private static EditionTwin? Confirm(
        string name,
        string? twinId,
        Dictionary<string, EditionTwin> twins) =>
        twinId is not null
        && name.Length > 0
        && twins.TryGetValue(twinId, out var twin)
        && string.Equals(twin.Name, name, StringComparison.Ordinal)
            ? twin
            : null;
}
