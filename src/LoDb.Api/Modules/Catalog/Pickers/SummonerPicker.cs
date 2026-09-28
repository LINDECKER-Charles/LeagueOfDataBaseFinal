using LoDb.Api.Modules.Catalog.Pickers.Options;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Pickers;

/// <summary>
/// <c>GET /api/pickers/summoners</c>: the summoner spells of the standard Summoner's Rift
/// queue (Data Dragon's CLASSIC mode), by name. Classic twins and event spells are left out.
/// </summary>
internal sealed record SummonerPicker
{
    private const string RequiredMode = "CLASSIC";

    public required string Version { get; init; }

    public required string Language { get; init; }

    public required IReadOnlyList<SummonerOption> Options { get; init; }

    public static IReadOnlyList<SummonerSpell> Pickable(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. catalog.Summoners.Entries.Where(static spell =>
            spell.Modes.Contains(RequiredMode, StringComparer.Ordinal))];
    }

    public static SummonerPicker Of(CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new SummonerPicker
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            Options = [.. Pickable(catalog)
                .Select(spell => SummonerOption.Of(spell, images))
                .OrderBy(static option => option.Name, StringComparer.Ordinal)],
        };
    }
}
