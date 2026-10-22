using LoDb.Domain.Catalog.Runes;

namespace LoDb.Domain.Tests.Catalog.Samples;

/// <summary>
/// Rune paths reduced to what a test states; everything else is neutral.
/// </summary>
internal static class RuneTreeSamples
{
    /// <summary>A path whose rows hold the runes given, one row per array.</summary>
    internal static RuneTree Named(int id, string name, params Rune[][] rows) => new()
    {
        Id = id,
        Key = name,
        Name = name,
        Icon = "perk-images/Styles/" + name + ".png",
        Slots = [.. rows.Select(static runes => new RuneSlot { Runes = runes })],
    };

    internal static Rune Rune(int id, string name, string longDesc = "") => new()
    {
        Id = id,
        Key = name,
        Name = name,
        Icon = "perk-images/" + name + ".png",
        ShortDesc = string.Empty,
        LongDesc = longDesc,
    };
}
