using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Derived.Items;
using LoDb.Domain.Editions;
using LoDb.Ingestion.Ddragon.Raw;
using LoDb.Ingestion.Ddragon.Raw.Items;

namespace LoDb.Ingestion.Normalization.Mapping;

/// <summary>
/// <c>item.json</c> to <see cref="Item"/> entries, twins linked (UP 6).
/// </summary>
/// <remarks>
/// Debris stays in the dataset, named by <see cref="ItemDebris.DisplayName"/>: recipes read
/// it, and <see cref="ItemDebris.IsDebris"/> hides it from browsing (UP 10).
/// </remarks>
internal static class ItemMapper
{
    public static IReadOnlyList<Item> Map(RawDataFile<RawItem> file)
    {
        var items = (file.Data ?? [])
            .Where(static entry => entry.Value is not null)
            .Select(static entry => Map(entry.Key, entry.Value!))
            .ToList();
        return EditionTwins.LinkItems(items);
    }

    private static Item Map(string id, RawItem item) => new()
    {
        Id = id,
        Name = ItemDebris.DisplayName(item.Name),
        Description = RawValues.Text(item.Description),
        Plaintext = RawValues.Text(item.Plaintext),
        Image = RawValues.ImageFile(item.Image),
        Gold = Gold(item.Gold),
        From = RawValues.Strings(item.From),
        Into = RawValues.Strings(item.Into),
        Depth = item.Depth,
        Stats = RawValues.Numbers(item.Stats),
        Maps = RawValues.Flags(item.Maps),
        Tags = RawValues.Strings(item.Tags),
        IsConsumed = item.Consumed ?? false,
        IsHiddenFromAll = item.HideFromAll ?? false,
        RequiredChampion = RawValues.Reference(item.RequiredChampion),
        RequiredAlly = RawValues.Reference(item.RequiredAlly),
    };

    private static ItemGold Gold(RawItemGold? gold) => new()
    {
        Base = gold?.Base ?? 0,
        Total = gold?.Total ?? 0,
        Sell = gold?.Sell ?? 0,
        IsPurchasable = gold?.Purchasable ?? false,
    };
}
