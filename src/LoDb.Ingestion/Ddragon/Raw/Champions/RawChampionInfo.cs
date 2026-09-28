namespace LoDb.Ingestion.Ddragon.Raw.Champions;

/// <summary>
/// The <c>info</c> ratings of a champion.
/// </summary>
internal sealed record RawChampionInfo
{
    public int? Attack { get; init; }

    public int? Defense { get; init; }

    public int? Magic { get; init; }

    public int? Difficulty { get; init; }
}
