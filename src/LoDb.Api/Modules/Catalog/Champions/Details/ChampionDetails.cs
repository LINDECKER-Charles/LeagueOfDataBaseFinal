using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Text;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Champions.Details;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/champions/{id}</c>: the card, then the lore, the
/// abilities and the number of skins, every image resolved and every art hotlinked, and the
/// champions on either side of it in the list.
/// </summary>
/// <remarks>
/// The skins themselves are read apart (<see cref="ChampionSkins"/>), by the gallery alone.
/// </remarks>
internal sealed record ChampionDetails
{
    private const int BaseSkin = 0;

    public required string Version { get; init; }

    public required string Language { get; init; }

    public string? ContentLanguage { get; init; }

    /// <summary>The page the routing redirects a stale or slugless URL to.</summary>
    public required string CanonicalPath { get; init; }

    /// <summary>The facts of the list's card.</summary>
    public required ChampionCard Profile { get; init; }

    public required string Blurb { get; init; }

    public string? Lore { get; init; }

    public required IReadOnlyList<string> AllyTips { get; init; }

    public required IReadOnlyList<string> EnemyTips { get; init; }

    /// <summary>The passive, then the spells in their Q, W, E, R order.</summary>
    public required IReadOnlyList<ChampionAbility> Abilities { get; init; }

    /// <summary>The base skin's art.</summary>
    public required ChampionArt Art { get; init; }

    /// <summary>
    /// The skins besides the base one, which the hero wears: those the gallery shows once it
    /// reads them from <c>…/skins</c>.
    /// </summary>
    public required int AlternateSkinCount { get; init; }

    /// <summary>The champions before and after it in the list's order.</summary>
    public required DetailNeighbours Neighbours { get; init; }

    public static ChampionDetails Of(
        ChampionDetail champion,
        CatalogSnapshot catalog,
        ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(champion);
        ArgumentNullException.ThrowIfNull(catalog);
        var profile = ChampionCard.Of(champion, catalog, images);
        return new ChampionDetails
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = catalog.Champions.ContentLanguage?.Code,
            CanonicalPath = profile.CanonicalPath,
            Profile = profile,
            Blurb = DdragonText.Clean(champion.Summary.Blurb),
            Lore = champion.Lore is { } lore ? DdragonText.Clean(lore) : null,
            AllyTips = champion.AllyTips,
            EnemyTips = champion.EnemyTips,
            Abilities = ChampionAbility.AllOf(champion, images),
            Art = ChampionArt.Of(profile.Id, BaseSkin),
            AlternateSkinCount = champion.Skins.Count(static skin => skin.Number != BaseSkin),
            Neighbours = ChampionList.NeighboursOf(champion, catalog),
        };
    }
}
