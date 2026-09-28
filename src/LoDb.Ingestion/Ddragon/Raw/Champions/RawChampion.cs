namespace LoDb.Ingestion.Ddragon.Raw.Champions;

/// <summary>
/// A champion of <c>championFull.json</c>, <c>champion.json</c> or <c>champion/{id}.json</c>:
/// the summary files only carry the fields up to <see cref="Stats"/>.
/// </summary>
internal sealed record RawChampion
{
    public string? Id { get; init; }

    public string? Key { get; init; }

    public string? Name { get; init; }

    public string? Title { get; init; }

    public string? Blurb { get; init; }

    public RawImage? Image { get; init; }

    public List<string?>? Tags { get; init; }

    public string? Partype { get; init; }

    public RawChampionInfo? Info { get; init; }

    public Dictionary<string, double?>? Stats { get; init; }

    public string? Lore { get; init; }

    public List<string?>? AllyTips { get; init; }

    public List<string?>? EnemyTips { get; init; }

    public List<RawChampionSpell?>? Spells { get; init; }

    public RawChampionPassive? Passive { get; init; }

    public List<RawChampionSkin?>? Skins { get; init; }
}
