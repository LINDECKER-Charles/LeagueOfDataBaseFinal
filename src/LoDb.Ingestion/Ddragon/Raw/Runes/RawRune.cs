namespace LoDb.Ingestion.Ddragon.Raw.Runes;

/// <summary>
/// A rune; its icon is a dead <c>.dds</c> path from 7.22 to 8.7 (UP 5).
/// </summary>
internal sealed record RawRune
{
    public int? Id { get; init; }

    public string? Key { get; init; }

    public string? Name { get; init; }

    public string? Icon { get; init; }

    public string? ShortDesc { get; init; }

    public string? LongDesc { get; init; }
}
