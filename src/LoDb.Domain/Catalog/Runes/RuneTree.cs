namespace LoDb.Domain.Catalog.Runes;

/// <summary>
/// A rune path ("Precision", id 8000) as <c>runesReforged.json</c> describes it.
/// </summary>
public sealed record RuneTree
{
    public required int Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>Icon path relative to Data Dragon's unversioned <c>img/</c> root (UP 5).</summary>
    public required string Icon { get; init; }

    /// <summary>Keystone row first, then the minor rows.</summary>
    public required IReadOnlyList<RuneSlot> Slots { get; init; }
}
