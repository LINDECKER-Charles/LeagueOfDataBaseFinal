using LoDb.Api.Modules.Catalog.Pickers.Options;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Pickers;

/// <summary>
/// <c>GET /api/pickers/runes</c>: every rune path in the upstream order; none before 7.22.1.
/// </summary>
internal sealed record RunePicker
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    public required IReadOnlyList<RuneTreeOption> Trees { get; init; }

    public static RunePicker Of(CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new RunePicker
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            Trees = [.. catalog.Runes.Entries.Select(tree => RuneTreeOption.Of(tree, images))],
        };
    }
}
