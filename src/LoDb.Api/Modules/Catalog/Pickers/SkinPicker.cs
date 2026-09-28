using LoDb.Api.Modules.Catalog.Pickers.Options;
using LoDb.Domain.Catalog.Champions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Pickers;

/// <summary>
/// <c>GET /api/pickers/skins?champion=</c>: the skins of a champion, chromas left out. A
/// missing or unknown champion gives no skins rather than an error: the banner picker then
/// shows an empty choice instead of a broken dialog.
/// </summary>
internal sealed record SkinPicker
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    /// <summary>The champion served; null when the one asked for is unknown.</summary>
    public string? Champion { get; init; }

    public required IReadOnlyList<SkinOption> Skins { get; init; }

    public static SkinPicker Of(CatalogSnapshot catalog, string? championId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var champion = Find(catalog, championId);
        return new SkinPicker
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            Champion = champion?.Summary.Id,
            Skins = champion is null
                ? []
                : [.. champion.Skins.Select(skin => SkinOption.Of(skin, champion.Summary))],
        };
    }

    private static ChampionDetail? Find(CatalogSnapshot catalog, string? championId) =>
        string.IsNullOrWhiteSpace(championId) ? null : catalog.Champions.Find(championId.Trim());
}
