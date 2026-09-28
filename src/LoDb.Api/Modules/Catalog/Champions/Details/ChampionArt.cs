using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog.Hotlinks;

namespace LoDb.Api.Modules.Catalog.Champions.Details;

/// <summary>
/// The three arts of a skin, hotlinked from Data Dragon rather than ingested (UP 8).
/// </summary>
internal sealed record ChampionArt
{
    public required Uri Splash { get; init; }

    public required Uri Loading { get; init; }

    public required Uri Centered { get; init; }

    public static ChampionArt Of(string championId, int skinNumber) => new()
    {
        Splash = ChampionHotlinks.Art(championId, ChampionArtKind.Splash, skinNumber),
        Loading = ChampionHotlinks.Art(championId, ChampionArtKind.Loading, skinNumber),
        Centered = ChampionHotlinks.Art(championId, ChampionArtKind.Centered, skinNumber),
    };
}
