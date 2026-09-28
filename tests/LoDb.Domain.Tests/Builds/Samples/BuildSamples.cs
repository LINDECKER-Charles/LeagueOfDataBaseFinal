using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Builds.Samples;

/// <summary>
/// The patch and the build of the legacy validator tests: two champions, three rune trees,
/// four items, and a structure every rule accepts.
/// </summary>
internal static class BuildSamples
{
    public const int Precision = 8000;
    public const int Domination = 8100;
    public const int Sorcery = 8200;

    public static readonly IReadOnlyList<RuneTree> Trees =
    [
        Tree(Precision, [[8005, 8008], [9101, 9111], [9104, 9105], [8014, 8017]]),
        Tree(Domination, [[8112, 8124], [8126, 8139], [8138, 8135], [8106, 8105]]),
        Tree(Sorcery, [[8214, 8229], [8224, 8226], [8210, 8234], [8237, 8232]]),
    ];

    public static readonly IReadOnlyList<Item> Items =
    [
        ItemSamples.Named("1055", "Doran's Blade") with
        {
            Maps = ItemSamples.Maps((11, true), (12, true)),
        },
        ItemSamples.Named("3006", "Berserker Greaves") with
        {
            Maps = ItemSamples.Maps((11, true), (12, false)),
        },
        ItemSamples.Named("2003", "Health Potion"),
        ItemSamples.Named("3031", "Infinity Edge"),
    ];

    public static BuildCatalog Catalog { get; } = new(["Aatrox", "Ahri"], Trees, Items);

    public static RunePageInput Runes { get; } = new()
    {
        PrimaryStyleId = Precision,
        PrimarySelections = [8005, 9101, 9104, 8014],
        SecondaryStyleId = Domination,
        SecondarySelections = [8126, 8138],
    };

    /// <summary>A structure every rule accepts.</summary>
    public static BuildStructureInput Valid { get; } = new()
    {
        ChampionId = "Aatrox",
        Runes = Runes,
        Steps = [Step("Start", null, "1055", "2003"), Step("Core", "rush it", "3006", "3031")],
    };

    public static StepInput Step(string? label, string? note, params string?[] items) =>
        new() { Label = label, Note = note, Items = items };

    public static BuildStructureInput WithRunes(Func<RunePageInput, RunePageInput> change) =>
        Valid with { Runes = change(Runes) };

    public static BuildStructureInput WithFirstStep(Func<StepInput, StepInput> change) =>
        Valid with { Steps = [change(Valid.Steps![0]!), .. Valid.Steps.Skip(1)] };

    /// <summary>Five steps of eight items: exactly the build-wide cap.</summary>
    public static IEnumerable<StepInput> CappedSteps() =>
        Enumerable.Range(1, 5).Select(static n => Step($"S{n}", null, [.. Repeat("1055", 8)]));

    public static string[] Repeat(string itemId, int count) =>
        [.. Enumerable.Repeat(itemId, count)];

    private static RuneTree Tree(int id, int[][] slots) => new()
    {
        Id = id,
        Key = "Tree" + id,
        Name = "Tree " + id,
        Icon = $"perk-images/Styles/{id}.png",
        Slots = [.. slots.Select(Slot)],
    };

    private static RuneSlot Slot(int[] perks) => new() { Runes = [.. perks.Select(Perk)] };

    private static Rune Perk(int id) => new()
    {
        Id = id,
        Key = "p" + id,
        Name = "P" + id,
        Icon = "x.png",
        ShortDesc = string.Empty,
        LongDesc = string.Empty,
    };
}
