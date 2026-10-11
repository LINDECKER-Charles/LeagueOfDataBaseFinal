using LoDb.Domain.Catalog.Champions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Champions.Details;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/champions/{id}/skins</c>: the skins of a champion,
/// the base one first, each with its art and its chromas.
/// </summary>
/// <remarks>
/// Apart from the champion's page, which only counts them: the gallery reads them once the
/// reader scrolls to it, so the page's payload carries no art nor chroma it does not render.
/// </remarks>
internal sealed record ChampionSkins
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    /// <summary>The champion's public id, such as MonkeyKing.</summary>
    public required string Champion { get; init; }

    public required IReadOnlyList<ChampionSkin> Skins { get; init; }

    public static ChampionSkins Of(ChampionDetail champion, CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(champion);
        ArgumentNullException.ThrowIfNull(catalog);
        return new ChampionSkins
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            Champion = champion.Summary.Id,
            Skins = [.. champion.Skins.Select(skin => ChampionSkin.Of(skin, champion.Summary))],
        };
    }
}
