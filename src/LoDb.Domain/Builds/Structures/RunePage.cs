namespace LoDb.Domain.Builds.Structures;

/// <summary>
/// A rune page as stored: a primary tree with its keystone and three minors, a secondary tree
/// with two minors, every id a Data Dragon perk or style id.
/// </summary>
public sealed record RunePage
{
    /// <summary>The id of a style left unset: no tree carries it.</summary>
    public const int Unset = 0;

    /// <summary>A page nothing was picked on, which an import resets a stale page to.</summary>
    public static RunePage Blank { get; } = new()
    {
        PrimaryStyleId = Unset,
        PrimarySelections = [],
        SecondaryStyleId = Unset,
        SecondarySelections = [],
    };

    public required int PrimaryStyleId { get; init; }

    /// <summary>The picks of slots 0 to 3 of the primary tree, the keystone first.</summary>
    public required IReadOnlyList<int> PrimarySelections { get; init; }

    public required int SecondaryStyleId { get; init; }

    /// <summary>Two picks of distinct minor rows of the secondary tree.</summary>
    public required IReadOnlyList<int> SecondarySelections { get; init; }
}
