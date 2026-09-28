namespace LoDb.Domain.Catalog.Runes;

/// <summary>
/// One row of a rune path: the player picks one rune of it.
/// </summary>
public sealed record RuneSlot
{
    public required IReadOnlyList<Rune> Runes { get; init; }
}
