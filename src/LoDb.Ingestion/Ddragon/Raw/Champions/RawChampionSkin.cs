namespace LoDb.Ingestion.Ddragon.Raw.Champions;

/// <summary>
/// A skin as Data Dragon lists it: 0.x versions write a null id, versions before about
/// 3.13.24 omit <c>num</c> (UP 3).
/// </summary>
internal sealed record RawChampionSkin
{
    public string? Id { get; init; }

    public int? Num { get; init; }

    public string? Name { get; init; }
}
