using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Champions;

namespace LoDb.Api.Modules.Profiles.Cards.Builds;

/// <summary>A public build on the card, its champion resolved on the profile's patch.</summary>
internal sealed record ProfileBuildCard
{
    /// <summary>The token of its <c>/b/{token}</c> link.</summary>
    public required string ShareToken { get; init; }

    public required string Name { get; init; }

    public required string ChampionId { get; init; }

    /// <summary>
    /// Null when the profile's patch lacks the champion: the card shows initials.
    /// </summary>
    public required string? ChampionName { get; init; }

    public required CatalogImage ChampionImage { get; init; }

    /// <summary>The patch the build is pinned to.</summary>
    public required string GameVersion { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public static ProfileBuildCard Of(PublicBuildRow row, ChampionDetail? champion, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(images);
        return new ProfileBuildCard
        {
            ShareToken = row.ShareToken,
            Name = row.Name,
            ChampionId = row.ChampionId,
            ChampionName = champion?.Summary.Name,
            ChampionImage = champion is null
                ? CatalogImage.Absent
                : images.Of(EntityImages.Portrait(champion)),
            GameVersion = row.GameVersion,
            UpdatedAt = row.UpdatedAt,
        };
    }
}
