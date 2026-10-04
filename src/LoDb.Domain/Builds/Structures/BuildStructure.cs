namespace LoDb.Domain.Builds.Structures;

/// <summary>
/// The canonical structure of a build, as stored: its champion, its rune page and its
/// purchase order.
/// </summary>
public sealed record BuildStructure
{
    /// <summary>The Data Dragon champion id, such as <c>MonkeyKing</c>; empty when unset.</summary>
    public required string ChampionId { get; init; }

    public required RunePage Runes { get; init; }

    public required IReadOnlyList<BuildStep> Steps { get; init; }
}
