using System.Text.Json.Serialization;

namespace LoDb.Ingestion.Ddragon.Raw.Champions;

/// <summary>
/// A champion ability; tooltips and effect tables are not read (see <c>ChampionSpell</c>).
/// </summary>
internal sealed record RawChampionSpell
{
    public string? Id { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public RawImage? Image { get; init; }

    public string? CooldownBurn { get; init; }

    public string? CostBurn { get; init; }

    public string? RangeBurn { get; init; }

    public int? MaxRank { get; init; }

    /// <summary>A string in the files ("-1"), missing before 4.x.</summary>
    [JsonConverter(typeof(LenientIntConverter))]
    public int? MaxAmmo { get; init; }
}
