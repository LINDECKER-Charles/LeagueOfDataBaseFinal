using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Text;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Champions.Details;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/champions/{id}</c>: the card, then the lore, the
/// abilities and the skins, every image resolved and every art hotlinked.
/// </summary>
internal sealed record ChampionDetails
{
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

    public required IReadOnlyList<ChampionSkin> Skins { get; init; }

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
            Art = ChampionArt.Of(profile.Id, 0),
            Skins = [.. champion.Skins.Select(skin => ChampionSkin.Of(skin, champion.Summary))],
        };
    }
}
