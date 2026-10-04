using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Profiles.Favorites;
using LoDb.Api.Modules.Profiles.Favorites.Views;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Ingestion.Catalog;

namespace LoDb.Api.Modules.Profiles.Showcase;

/// <summary>
/// What a profile curates, resolved on its patch: the four favorites and the skin banner.
/// </summary>
/// <remarks>
/// Display is best effort: without a catalog every stored favorite shows as unavailable, and
/// nothing stored is touched.
/// </remarks>
internal sealed record ProfileShowcase
{
    /// <summary>
    /// The version the favorites resolved on; null when no catalog could be read.
    /// </summary>
    public required string? Version { get; init; }

    /// <summary>
    /// The Data Dragon language of the names; null when no catalog could be read.
    /// </summary>
    public required string? Language { get; init; }

    /// <summary>
    /// False when the catalog could not be read: the favorites then show as unavailable for
    /// that reason, not because the patch lacks them.
    /// </summary>
    public required bool IsCatalogAvailable { get; init; }

    public required FavoriteBoard Favorites { get; init; }

    public required SkinBanner? Skin { get; init; }

    /// <param name="user">The profile's account.</param>
    /// <param name="context">The catalog of its patch; null when none could be read.</param>
    /// <param name="cancellationToken">Aborts the wait for the images.</param>
    public static async Task<ProfileShowcase> ResolveAsync(
        User user,
        CatalogContext? context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var ids = FavoriteIds.Of(user);
        var entries = FavoriteBoard.Find(ids, context?.Catalog);
        var images = context is null
            ? ImageSet.Empty
            : await context.ResolveAsync(
                FavoriteBoard.Images(entries),
                ColdDemand.Synchronous,
                cancellationToken);
        return new ProfileShowcase
        {
            Version = context?.Catalog.Version.Value,
            Language = context?.Catalog.Language.Code,
            IsCatalogAvailable = context is not null,
            Favorites = FavoriteBoard.Of(ids, entries, images),
            Skin = SkinBanner.Of(user.FavoriteSkinId, context?.Catalog),
        };
    }
}
