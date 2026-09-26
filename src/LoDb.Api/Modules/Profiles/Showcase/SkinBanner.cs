using LoDb.Api.Modules.Profiles.Favorites;
using LoDb.Domain.Derived.Champions;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog.Hotlinks;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Profiles.Showcase;

/// <summary>
/// The favorite skin of a profile, its art hotlinked from the id alone (UP 8): a catalog that
/// cannot be read only costs its name, which falls back to the champion id.
/// </summary>
internal sealed record SkinBanner
{
    /// <summary>The stored id, such as <c>Ahri_7</c>.</summary>
    public required string Id { get; init; }

    public required string ChampionId { get; init; }

    public required int Number { get; init; }

    /// <summary>
    /// The skin's name on the resolved patch; the champion id when unknown there.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The centered splash, the banner itself.</summary>
    public required Uri Banner { get; init; }

    /// <summary>The wide splash, shown when the centered one fails to load.</summary>
    public required Uri Splash { get; init; }

    /// <summary>The banner of <paramref name="storedId"/>; null when none or malformed.</summary>
    public static SkinBanner? Of(string? storedId, CatalogSnapshot? catalog)
    {
        if (!SkinId.TryParse(storedId, out var skin))
        {
            return null;
        }

        return new SkinBanner
        {
            Id = storedId!,
            ChampionId = skin.ChampionId,
            Number = skin.Number,
            Name = NameOf(skin, catalog) ?? skin.ChampionId,
            Banner = ChampionHotlinks.Art(skin.ChampionId, ChampionArtKind.Centered, skin.Number),
            Splash = ChampionHotlinks.Art(skin.ChampionId, ChampionArtKind.Splash, skin.Number),
        };
    }

    private static string? NameOf(SkinId skin, CatalogSnapshot? catalog)
    {
        var champion = catalog?.Champions.Find(skin.ChampionId);
        var found = champion?.Skins.FirstOrDefault(entry => entry.Number == skin.Number);
        return found is null ? null : SkinName.Display(found, champion!.Summary.Name);
    }
}
