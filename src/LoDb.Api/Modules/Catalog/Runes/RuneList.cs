using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Runes;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/runes</c>: the runes of every path, path by path and
/// row by row; empty before 7.22.1, which shipped the first rune paths (UP 1).
/// </summary>
internal sealed record RuneList
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    /// <summary>Null when the version ships no rune path at all.</summary>
    public string? ContentLanguage { get; init; }

    /// <summary>Runes of the whole list, whatever the page.</summary>
    public required int Total { get; init; }

    public int? Page { get; init; }

    public int? Size { get; init; }

    /// <summary>Every path, whatever the page: the path facet and its links.</summary>
    public required IReadOnlyList<RuneTreeCard> Trees { get; init; }

    public required IReadOnlyList<RuneCard> Entries { get; init; }

    /// <summary>Every rune with its path and row, in the list's order.</summary>
    public static IReadOnlyList<RuneLocation> Located(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. catalog.Runes.Entries.SelectMany(static tree => tree.Slots.SelectMany(
            (slot, index) => slot.Runes.Select(rune => new RuneLocation
            {
                Tree = tree,
                SlotIndex = index,
                Rune = rune,
            })))];
    }

    public static RuneList Of(CatalogSnapshot catalog, PageRequest page, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(page);
        var runes = Located(catalog);
        return new RuneList
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = catalog.Runes.ContentLanguage?.Code,
            Total = runes.Count,
            Page = page.Page,
            Size = page.Size,
            Trees = [.. catalog.Runes.Entries
                .Select(tree => RuneTreeCard.Of(tree, catalog, images))],
            Entries = [.. page.Slice(runes).Select(rune => RuneCard.Of(rune, catalog, images))],
        };
    }
}
