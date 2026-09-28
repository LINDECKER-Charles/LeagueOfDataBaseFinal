using LoDb.Domain.Catalog.Runes;

namespace LoDb.Ingestion.Catalog.Snapshots;

/// <summary>A rune, with the path and the row it sits in.</summary>
public sealed record RuneLocation
{
    public required RuneTree Tree { get; init; }

    /// <summary>Index of the row in the path: 0 for the keystones.</summary>
    public required int SlotIndex { get; init; }

    public required Rune Rune { get; init; }
}
