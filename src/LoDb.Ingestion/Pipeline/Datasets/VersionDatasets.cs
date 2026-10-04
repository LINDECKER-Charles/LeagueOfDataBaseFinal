using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Ingestion.Normalization;

namespace LoDb.Ingestion.Pipeline.Datasets;

/// <summary>The four stored datasets of one (version, language).</summary>
internal sealed record VersionDatasets
{
    public required DatasetDocument<ChampionDetail> Champions { get; init; }

    public required DatasetDocument<Item> Items { get; init; }

    public required DatasetDocument<RuneTree> Runes { get; init; }

    public required DatasetDocument<SummonerSpell> Summoners { get; init; }
}
