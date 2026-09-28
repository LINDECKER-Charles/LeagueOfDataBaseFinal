namespace LoDb.Domain.Catalog.Items;

/// <summary>
/// Price of an item: <see cref="Base"/> is the combine cost on top of the components,
/// <see cref="Total"/> the full price.
/// </summary>
public sealed record ItemGold
{
    public required int Base { get; init; }

    public required int Total { get; init; }

    public required int Sell { get; init; }

    public required bool IsPurchasable { get; init; }
}
