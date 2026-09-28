namespace LoDb.Domain.Builds.Editing;

/// <summary>
/// A rune page being edited: unlike a stored page, it knows which slot each pick fills and
/// in which order the secondary picks were made.
/// </summary>
public sealed record RuneDraft
{
    /// <summary>Null until a primary tree is chosen.</summary>
    public int? PrimaryStyleId { get; init; }

    /// <summary>One entry per primary slot, the keystone first; null for an empty slot.</summary>
    public required IReadOnlyList<int?> PrimaryPerks { get; init; }

    /// <summary>Null until a secondary tree is chosen.</summary>
    public int? SecondaryStyleId { get; init; }

    /// <summary>The secondary picks, oldest first: the next one evicts the first.</summary>
    public required IReadOnlyList<SecondaryPick> SecondaryPicks { get; init; }
}
