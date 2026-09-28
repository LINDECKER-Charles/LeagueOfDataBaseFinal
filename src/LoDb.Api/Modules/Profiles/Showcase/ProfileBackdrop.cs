using LoDb.Api.Modules.Profiles.Favorites;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog.Hotlinks;

namespace LoDb.Api.Modules.Profiles.Showcase;

/// <summary>The art behind a profile's header; a page without one draws its gradient.</summary>
internal sealed record ProfileBackdrop
{
    /// <summary>The centered splash.</summary>
    public required Uri Image { get; init; }

    /// <summary>The wide splash, shown when the centered one fails to load.</summary>
    public required Uri Fallback { get; init; }

    public required BackdropKind Kind { get; init; }

    /// <summary>
    /// The public card's backdrop: the favorite skin, else the favorite champion's base skin.
    /// </summary>
    public static ProfileBackdrop? Of(SkinBanner? skin, string? favoriteChampionId) =>
        skin is null
            ? OfChampion(favoriteChampionId)
            : new ProfileBackdrop
            {
                Image = skin.Banner,
                Fallback = skin.Splash,
                Kind = BackdropKind.Skin,
            };

    /// <summary>
    /// The owner's page backdrop: always the favorite champion, since the skin is what the
    /// public card shows off.
    /// </summary>
    public static ProfileBackdrop? OfChampion(string? favoriteChampionId)
    {
        if (SkinId.BaseOf(favoriteChampionId) is not { } skin)
        {
            return null;
        }

        return new ProfileBackdrop
        {
            Image = ChampionHotlinks.Art(skin.ChampionId, ChampionArtKind.Centered, skin.Number),
            Fallback = ChampionHotlinks.Art(skin.ChampionId, ChampionArtKind.Splash, skin.Number),
            Kind = BackdropKind.Champion,
        };
    }
}
