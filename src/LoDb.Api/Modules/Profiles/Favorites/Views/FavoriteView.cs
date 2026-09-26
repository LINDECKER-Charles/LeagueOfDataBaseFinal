using LoDb.Api.Modules.Catalog.Reading;

namespace LoDb.Api.Modules.Profiles.Favorites.Views;

/// <summary>A favorite slot: what is stored, and how the resolved patch shows it.</summary>
internal sealed record FavoriteView
{
    /// <summary>
    /// The id stored, whatever the patch: a form sends it back to keep an unavailable
    /// favorite, never the null of <see cref="Current"/>.
    /// </summary>
    public required string? StoredId { get; init; }

    public required FavoriteStatus Status { get; init; }

    /// <summary>
    /// The favorite on the resolved patch; null unless <see cref="Status"/> is resolved.
    /// </summary>
    public required ResolvedFavorite? Current { get; init; }

    public static FavoriteView Of(string? storedId, FavoriteEntry? entry, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (string.IsNullOrEmpty(storedId))
        {
            return new FavoriteView
            {
                StoredId = null,
                Status = FavoriteStatus.Empty,
                Current = null,
            };
        }

        return new FavoriteView
        {
            StoredId = storedId,
            Status = entry is null ? FavoriteStatus.Unavailable : FavoriteStatus.Resolved,
            Current = entry is null
                ? null
                : new ResolvedFavorite
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Image = images.Of(entry.Image),
                },
        };
    }
}
