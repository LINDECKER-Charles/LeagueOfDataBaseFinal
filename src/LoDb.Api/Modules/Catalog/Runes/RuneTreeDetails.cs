using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Derived.Runes;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Runes;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/runes/{id}</c>: a rune path and its rows, every image
/// resolved.
/// </summary>
internal sealed record RuneTreeDetails
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    public string? ContentLanguage { get; init; }

    /// <summary>The page the routing redirects a stale or slugless URL to.</summary>
    public required string CanonicalPath { get; init; }

    public required RuneTreeCard Profile { get; init; }

    public required IReadOnlyList<RuneRow> Slots { get; init; }

    public static RuneTreeDetails Of(RuneTree tree, CatalogSnapshot catalog, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(catalog);
        var profile = RuneTreeCard.Of(tree, catalog, images);
        return new RuneTreeDetails
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = catalog.Runes.ContentLanguage?.Code,
            CanonicalPath = profile.CanonicalPath,
            Profile = profile,
            Slots = [.. tree.Slots.Select((slot, index) => new RuneRow
            {
                Slot = RuneSlotToken.Of(index),
                Runes = [.. slot.Runes.Select(rune => RuneEntry.Of(rune, images))],
            })],
        };
    }
}
