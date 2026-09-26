namespace LoDb.Ingestion.Ddragon.Raw.Runes;

/// <summary>
/// One row of a rune path.
/// </summary>
internal sealed record RawRuneSlot
{
    public List<RawRune?>? Runes { get; init; }
}
