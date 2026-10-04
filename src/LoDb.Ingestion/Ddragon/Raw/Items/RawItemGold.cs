namespace LoDb.Ingestion.Ddragon.Raw.Items;

/// <summary>
/// The <c>gold</c> node of an item.
/// </summary>
internal sealed record RawItemGold
{
    public int? Base { get; init; }

    public int? Total { get; init; }

    public int? Sell { get; init; }

    public bool? Purchasable { get; init; }
}
