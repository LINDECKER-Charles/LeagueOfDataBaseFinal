namespace LoDb.Api.Modules.Profiles.Favorites;

/// <summary>The four favorite slots of a profile, each backed by a <c>users</c> column.</summary>
internal enum FavoriteSlot
{
    /// <summary><c>favorite_champion_id</c>: a champion id, such as <c>Ahri</c>.</summary>
    Champion,

    /// <summary><c>favorite_item_id</c>: an item id, such as <c>3031</c>.</summary>
    Item,

    /// <summary><c>favorite_rune_id</c>: a rune or rune path id, such as <c>8112</c>.</summary>
    Rune,

    /// <summary>
    /// <c>favorite_summoner_id</c>: a summoner spell id, such as <c>SummonerFlash</c>.
    /// </summary>
    Summoner,
}
