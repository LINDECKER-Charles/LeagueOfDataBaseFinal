using LoDb.Domain.Catalog.Runes;
using LoDb.Ingestion.Ddragon.Raw.Runes;

namespace LoDb.Ingestion.Normalization.Mapping;

/// <summary>
/// <c>runesReforged.json</c> to <see cref="RuneTree"/> entries, in upstream order.
/// </summary>
/// <remarks>
/// Icons are kept as written: on 7.22.1 to 8.7.1 they point to <c>.dds</c> files Data
/// Dragon answers with 403 (UP 5), an absence the image ingestion records.
/// </remarks>
internal static class RuneMapper
{
    public static IReadOnlyList<RuneTree> Map(List<RawRuneTree?> trees) =>
        [.. trees.OfType<RawRuneTree>().Select(Tree)];

    private static RuneTree Tree(RawRuneTree tree) => new()
    {
        Id = tree.Id ?? 0,
        Key = RawValues.Text(tree.Key),
        Name = RawValues.Text(tree.Name),
        Icon = RawValues.Text(tree.Icon),
        Slots = [.. (tree.Slots ?? []).OfType<RawRuneSlot>().Select(Slot)],
    };

    private static RuneSlot Slot(RawRuneSlot slot) => new()
    {
        Runes = [.. (slot.Runes ?? []).OfType<RawRune>().Select(Rune)],
    };

    private static Rune Rune(RawRune rune) => new()
    {
        Id = rune.Id ?? 0,
        Key = RawValues.Text(rune.Key),
        Name = RawValues.Text(rune.Name),
        Icon = RawValues.Text(rune.Icon),
        ShortDesc = RawValues.Text(rune.ShortDesc),
        LongDesc = RawValues.Text(rune.LongDesc),
    };
}
