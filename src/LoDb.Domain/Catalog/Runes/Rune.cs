namespace LoDb.Domain.Catalog.Runes;

/// <summary>
/// A rune ("Press the Attack", id 8005).
/// </summary>
public sealed record Rune
{
    public required int Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>Icon path relative to Data Dragon's unversioned <c>img/</c> root (UP 5).</summary>
    public required string Icon { get; init; }

    public required string ShortDesc { get; init; }

    public required string LongDesc { get; init; }
}
