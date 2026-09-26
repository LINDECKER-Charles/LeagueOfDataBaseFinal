using LoDb.Api.Modules.Catalog.Pickers.Options;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Pickers;

/// <summary><c>GET /api/pickers/champions</c>: every champion, by name.</summary>
internal sealed record ChampionPicker
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    public required IReadOnlyList<ChampionOption> Options { get; init; }

    public static ChampionPicker Of(CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new ChampionPicker
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            Options = [.. catalog.Champions.Entries
                .Select(champion => ChampionOption.Of(champion, images))
                .OrderBy(static option => option.Name, StringComparer.Ordinal)],
        };
    }
}
