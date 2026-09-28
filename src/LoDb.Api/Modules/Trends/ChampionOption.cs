using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Trends;

/// <summary>A champion the trends may be filtered on.</summary>
internal sealed record ChampionOption
{
    public required string Id { get; init; }

    /// <summary>Its name on the version browsed; its id when that version lacks it.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The options of <paramref name="ids"/>, by name as the legacy facet sorted them.
    /// </summary>
    /// <param name="ids">Champion ids, sorted.</param>
    /// <param name="catalog">The version browsed; null when it could not be read.</param>
    public static IReadOnlyList<ChampionOption> Sorted(
        IEnumerable<string> ids,
        CatalogSnapshot? catalog) =>
        [
            .. ids
                .Select(id => new ChampionOption
                {
                    Id = id,
                    Name = catalog?.Champions.Find(id)?.Summary.Name ?? id,
                })
                .OrderBy(static option => option.Name, StringComparer.OrdinalIgnoreCase),
        ];
}
