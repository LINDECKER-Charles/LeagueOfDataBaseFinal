namespace LoDb.Api.Modules.Profiles.Favorites;

/// <summary>A favorite selection fit to store, and the slots it had to empty.</summary>
/// <param name="Values">The ids to store.</param>
/// <param name="Rejected">The slots whose submitted id was dropped, in slot order.</param>
internal sealed record SanitizedFavorites(FavoriteIds Values, IReadOnlyList<FavoriteSlot> Rejected);
