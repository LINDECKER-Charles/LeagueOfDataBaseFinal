using LoDb.Domain.Catalog.Champions;
using LoDb.Ingestion.Ddragon.Raw.Champions;

namespace LoDb.Ingestion.Normalization.Mapping;

/// <summary>
/// Raw champions to <see cref="ChampionDetail"/>: a summary-only entry (the per-champion file
/// is missing, UP 3) yields empty sections, as <see cref="ChampionDetail.FromSummary"/> does.
/// </summary>
internal static class ChampionMapper
{
    /// <summary>The champions that have an id, in upstream order.</summary>
    public static IReadOnlyList<ChampionDetail> Map(
        IEnumerable<RawChampion?> champions,
        ChromaCatalog chromas) =>
        [.. champions
            .Where(static champion => !string.IsNullOrEmpty(champion?.Id))
            .Select(champion => Detail(champion!, chromas))];

    private static ChampionDetail Detail(RawChampion champion, ChromaCatalog chromas) => new()
    {
        Summary = Summary(champion),
        Lore = champion.Lore,
        AllyTips = RawValues.Strings(champion.AllyTips),
        EnemyTips = RawValues.Strings(champion.EnemyTips),
        Spells = [.. (champion.Spells ?? []).OfType<RawChampionSpell>().Select(Spell)],
        Passive = Passive(champion.Passive),
        Skins = SkinMapper.Map(champion.Skins, chromas),
    };

    // 0.x ships no partype (UP 3): the absence stays a null, never an empty resource name.
    private static ChampionSummary Summary(RawChampion champion) => new()
    {
        Id = RawValues.Text(champion.Id),
        Key = RawValues.Text(champion.Key),
        Name = RawValues.Text(champion.Name),
        Title = RawValues.Text(champion.Title),
        Blurb = RawValues.Text(champion.Blurb),
        Image = RawValues.ImageFile(champion.Image),
        Tags = RawValues.Strings(champion.Tags),
        Partype = champion.Partype,
        Info = Ratings(champion.Info),
        Stats = RawValues.Numbers(champion.Stats),
    };

    private static ChampionRatings? Ratings(RawChampionInfo? info) =>
        info is null
            ? null
            : new ChampionRatings
            {
                Attack = info.Attack ?? 0,
                Defense = info.Defense ?? 0,
                Magic = info.Magic ?? 0,
                Difficulty = info.Difficulty ?? 0,
            };

    private static ChampionSpell Spell(RawChampionSpell spell) => new()
    {
        Id = RawValues.Text(spell.Id),
        Name = RawValues.Text(spell.Name),
        Description = RawValues.Text(spell.Description),
        Image = RawValues.ImageFile(spell.Image),
        CooldownBurn = spell.CooldownBurn,
        CostBurn = spell.CostBurn,
        RangeBurn = spell.RangeBurn,
        MaxRank = spell.MaxRank,
        MaxAmmo = spell.MaxAmmo,
    };

    private static ChampionPassive? Passive(RawChampionPassive? passive) =>
        passive is null
            ? null
            : new ChampionPassive
            {
                Name = RawValues.Text(passive.Name),
                Description = RawValues.Text(passive.Description),
                Image = RawValues.ImageFile(passive.Image),
            };
}
