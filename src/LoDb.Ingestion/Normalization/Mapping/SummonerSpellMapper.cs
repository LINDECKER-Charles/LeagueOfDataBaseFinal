using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Editions;
using LoDb.Ingestion.Ddragon.Raw;
using LoDb.Ingestion.Ddragon.Raw.Summoners;

namespace LoDb.Ingestion.Normalization.Mapping;

/// <summary>
/// <c>summoner.json</c> to <see cref="SummonerSpell"/> entries, twins linked (UP 6).
/// </summary>
internal static class SummonerSpellMapper
{
    public static IReadOnlyList<SummonerSpell> Map(RawDataFile<RawSummonerSpell> file)
    {
        var spells = (file.Data ?? [])
            .Where(static entry => entry.Value is not null)
            .Select(static entry => Map(entry.Key, entry.Value!))
            .ToList();
        return EditionTwins.LinkSummonerSpells(spells);
    }

    // The map key is the id; the entry repeats it, and wins when it does.
    private static SummonerSpell Map(string key, RawSummonerSpell spell) => new()
    {
        Id = string.IsNullOrEmpty(spell.Id) ? key : spell.Id,
        Key = RawValues.Text(spell.Key),
        Name = RawValues.Text(spell.Name),
        Description = RawValues.Text(spell.Description),
        Image = RawValues.ImageFile(spell.Image),
        Cooldown = spell.Cooldown ?? [],
        Cost = spell.Cost ?? [],
        Range = spell.Range ?? [],
        CostType = spell.CostType,
        SummonerLevel = spell.SummonerLevel,
        MaxAmmo = spell.MaxAmmo,
        Modes = RawValues.Strings(spell.Modes),
    };
}
