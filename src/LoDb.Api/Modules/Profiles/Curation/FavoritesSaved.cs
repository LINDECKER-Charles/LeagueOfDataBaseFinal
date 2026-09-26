using LoDb.Api.Modules.Profiles.Favorites;

namespace LoDb.Api.Modules.Profiles.Curation;

/// <summary>What a favorite save stored, and what it had to drop.</summary>
internal sealed record FavoritesSaved
{
    /// <summary>The ids now stored, unavailable ones kept included.</summary>
    public required FavoriteIds Favorites { get; init; }

    public required string? Skin { get; init; }

    /// <summary>
    /// The slots whose id the pinned version lacks, or too long: they are now empty.
    /// </summary>
    public required IReadOnlyList<FavoriteSlot> Rejected { get; init; }

    /// <summary>Whether the skin sent was malformed, and the skin slot is now empty.</summary>
    public required bool IsSkinRejected { get; init; }
}
