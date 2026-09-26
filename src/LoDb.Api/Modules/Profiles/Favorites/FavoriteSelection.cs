namespace LoDb.Api.Modules.Profiles.Favorites;

/// <summary>
/// Sanitizes a submitted favorite selection against the pinned patch, slot by slot:
/// <list type="bullet">
/// <item>blank clears the slot;</item>
/// <item>an id longer than its column is dropped and reported;</item>
/// <item>an id the patch holds is kept;</item>
/// <item>
/// an id the patch lacks but equal to the stored one is kept: the favorite is merely absent
/// from this patch, and wiping it would lose it for good;
/// </item>
/// <item>any other id is dropped and reported.</item>
/// </list>
/// </summary>
/// <remarks>
/// Pure: the patch is a predicate. A failure of the catalog is not caught here, since a save
/// must then be refused rather than wipe anything.
/// </remarks>
internal static class FavoriteSelection
{
    /// <param name="submitted">The ids sent, untrimmed.</param>
    /// <param name="stored">The ids stored now.</param>
    /// <param name="exists">Whether the pinned patch holds an id in a slot.</param>
    public static SanitizedFavorites Sanitize(
        FavoriteIds submitted,
        FavoriteIds stored,
        Func<FavoriteSlot, string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(submitted);
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(exists);
        var values = FavoriteIds.None;
        List<FavoriteSlot> rejected = [];
        foreach (var slot in Enum.GetValues<FavoriteSlot>())
        {
            var id = submitted.Get(slot)?.Trim() ?? string.Empty;
            var kept = Keep(slot, id, stored.Get(slot), exists);
            values = values.With(slot, kept);
            if (id.Length > 0 && kept is null)
            {
                rejected.Add(slot);
            }
        }

        return new SanitizedFavorites(values, rejected);
    }

    private static string? Keep(
        FavoriteSlot slot,
        string id,
        string? stored,
        Func<FavoriteSlot, string, bool> exists)
    {
        if (id.Length == 0 || id.Length > FavoriteIds.MaxLength(slot))
        {
            return null;
        }

        return exists(slot, id) || id == stored ? id : null;
    }
}
