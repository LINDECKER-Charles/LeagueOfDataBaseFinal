using System.Text.Json.Serialization;

namespace LoDb.Ingestion.Ddragon.Raw.Summoners;

/// <summary>
/// A summoner spell of <c>summoner.json</c>. Before 4.x, <c>range</c> may be the string
/// "self" instead of a list, and <c>modes</c> is missing.
/// </summary>
internal sealed record RawSummonerSpell
{
    public string? Id { get; init; }

    public string? Key { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public RawImage? Image { get; init; }

    [JsonConverter(typeof(LenientNumberListConverter))]
    public List<double>? Cooldown { get; init; }

    [JsonConverter(typeof(LenientNumberListConverter))]
    public List<double>? Cost { get; init; }

    [JsonConverter(typeof(LenientNumberListConverter))]
    public List<double>? Range { get; init; }

    public string? CostType { get; init; }

    public int? SummonerLevel { get; init; }

    /// <summary>A string in the files ("-1"), missing before 4.x.</summary>
    [JsonConverter(typeof(LenientIntConverter))]
    public int? MaxAmmo { get; init; }

    public List<string?>? Modes { get; init; }
}
