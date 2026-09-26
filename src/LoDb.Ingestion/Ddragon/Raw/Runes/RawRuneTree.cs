namespace LoDb.Ingestion.Ddragon.Raw.Runes;

/// <summary>
/// A rune path of <c>runesReforged.json</c>, a top-level list rather than a <c>data</c> map.
/// </summary>
internal sealed record RawRuneTree
{
    public int? Id { get; init; }

    public string? Key { get; init; }

    public string? Name { get; init; }

    public string? Icon { get; init; }

    public List<RawRuneSlot?>? Slots { get; init; }
}
