namespace LoDb.Ingestion.Ddragon.Raw.Champions;

/// <summary>
/// The innate ability of a champion.
/// </summary>
internal sealed record RawChampionPassive
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public RawImage? Image { get; init; }
}
