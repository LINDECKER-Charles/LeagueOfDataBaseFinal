using LoDb.Api.Modules.Profiles.Favorites;

namespace LoDb.Api.Modules.Profiles.Curation;

/// <summary>
/// Body of <c>PUT /api/profile/favorites</c>: every slot, stated. Null or blank clears a slot;
/// to keep a favorite the patch lacks, send back its <c>storedId</c>.
/// </summary>
internal sealed record SaveFavoritesRequest
{
    /// <summary>A champion id, such as <c>Ahri</c>.</summary>
    public required string? Champion { get; init; }

    /// <summary>An item id, such as <c>3031</c>.</summary>
    public required string? Item { get; init; }

    /// <summary>The id of a rune or of a rune path, such as <c>8112</c>.</summary>
    public required string? Rune { get; init; }

    /// <summary>A summoner spell id, such as <c>SummonerFlash</c>.</summary>
    public required string? Summoner { get; init; }

    /// <summary>A skin as <c>{championId}_{number}</c>, such as <c>Ahri_7</c>.</summary>
    public required string? Skin { get; init; }

    public FavoriteIds Ids => new()
    {
        Champion = Champion,
        Item = Item,
        Rune = Rune,
        Summoner = Summoner,
    };
}
