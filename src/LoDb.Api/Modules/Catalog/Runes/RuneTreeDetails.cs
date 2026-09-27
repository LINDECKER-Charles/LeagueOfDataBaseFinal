using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Derived.Runes;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Runes;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/runes/{id}</c>: a rune path and its rows, every image
/// resolved, and the paths on either side of it in the list.
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

    /// <summary>The paths before and after it in the list's order.</summary>
    public required DetailNeighbours Neighbours { get; init; }

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
            Neighbours = RuneList.NeighboursOf(tree, catalog),
        };
    }
}
