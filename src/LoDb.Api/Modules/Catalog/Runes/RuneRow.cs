namespace LoDb.Api.Modules.Catalog.Runes;

/// <summary>A row of a rune path: the keystones, then three rows of minor runes.</summary>
internal sealed record RuneRow
{
    /// <summary>"keystone", then "row1" to "row3".</summary>
    public required string Slot { get; init; }

    public required IReadOnlyList<RuneEntry> Runes { get; init; }
}
