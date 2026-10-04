using LoDb.Domain.Catalog.Items;

namespace LoDb.Domain.Tests.Catalog.Samples;

/// <summary>
/// Items reduced to what a test states; everything else is neutral.
/// </summary>
internal static class ItemSamples
{
    /// <summary>A purchasable item without recipe, stats nor map flags.</summary>
    internal static Item Named(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Description = string.Empty,
        Plaintext = string.Empty,
        Image = id + ".png",
        Gold = Gold(total: 0, combine: 0),
        From = [],
        Into = [],
        Stats = new Dictionary<string, double>(),
        Maps = new Dictionary<int, bool>(),
        Tags = [],
    };

    internal static ItemGold Gold(int total, int combine) =>
        new() { Base = combine, Total = total, Sell = 0, IsPurchasable = true };

    internal static Dictionary<int, bool> Maps(params (int MapId, bool IsAvailable)[] flags) =>
        flags.ToDictionary(flag => flag.MapId, flag => flag.IsAvailable);
}
