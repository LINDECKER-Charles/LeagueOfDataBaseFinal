namespace LoDb.Domain.Builds.Import;

/// <summary>What an import to another patch had to change, for the author to review.</summary>
public sealed record ImportReport
{
    /// <summary>
    /// Whether the target patch lacks the champion; its id is kept, so the build shows it as
    /// a ghost until the author picks another.
    /// </summary>
    public required bool ChampionMissing { get; init; }

    /// <summary>Whether the rune page was blanked because one of its ids is gone.</summary>
    public required bool RunesReset { get; init; }

    /// <summary>The items left out, in step order.</summary>
    public required IReadOnlyList<DroppedItem> DroppedItems { get; init; }
}
