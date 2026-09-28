using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Ingestion.Normalization.Serialization;

namespace LoDb.Ingestion.Normalization;

/// <summary>
/// The datasets of one (version, language): one file per resource, champions with their
/// details and chromas included.
/// </summary>
public static class DatasetTypes
{
    public static DatasetType<ChampionDetail> Champions { get; } =
        new("champions", DatasetSerializer.Contract<ChampionDetail>());

    public static DatasetType<Item> Items { get; } =
        new("items", DatasetSerializer.Contract<Item>());

    public static DatasetType<RuneTree> Runes { get; } =
        new("runes", DatasetSerializer.Contract<RuneTree>());

    public static DatasetType<SummonerSpell> Summoners { get; } =
        new("summoners", DatasetSerializer.Contract<SummonerSpell>());
}
