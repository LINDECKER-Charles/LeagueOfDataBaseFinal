namespace LoDb.Ingestion.Ddragon.Raw;

/// <summary>
/// A Data Dragon file whose entries sit in an id-keyed <c>data</c> map (champions, items,
/// summoner spells), in upstream order.
/// </summary>
internal sealed record RawDataFile<TEntry>
{
    public Dictionary<string, TEntry?>? Data { get; init; }
}
