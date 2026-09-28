namespace LoDb.Domain.Builds.Structures;

/// <summary>A rune page as submitted or stored, read leniently.</summary>
public sealed record RunePageInput
{
    /// <summary>Null when absent or not an integer.</summary>
    public int? PrimaryStyleId { get; init; }

    /// <summary>
    /// Null when absent or not a list; an entry is null when it is not an integer.
    /// </summary>
    public IReadOnlyList<int?>? PrimarySelections { get; init; }

    /// <summary>Null when absent or not an integer.</summary>
    public int? SecondaryStyleId { get; init; }

    /// <summary>
    /// Null when absent or not a list; an entry is null when it is not an integer.
    /// </summary>
    public IReadOnlyList<int?>? SecondarySelections { get; init; }

    /// <summary>Every id the page could read, styles first, in page order.</summary>
    public IEnumerable<int> ReadableIds()
    {
        int?[] styles = [PrimaryStyleId, SecondaryStyleId];
        IEnumerable<int?> picks = [.. PrimarySelections ?? [], .. SecondarySelections ?? []];
        return styles.Concat(picks).OfType<int>();
    }
}
