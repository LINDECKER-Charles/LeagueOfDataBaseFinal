using System.Globalization;
using LoDb.Domain.Catalog;

namespace LoDb.Ingestion.Catalog.Snapshots;

/// <summary>
/// The searchable entries of a catalog, folded once when the catalog is built.
/// </summary>
/// <remarks>
/// As in the legacy search: champions, rune paths and summoner spells match on their id or
/// their name, items on their name alone (their ids are numbers) and debris items not at all
/// (UP 10). Hits keep the upstream order.
/// </remarks>
internal sealed class CatalogSearch
{
    private readonly IReadOnlyList<Candidate> candidates;

    public CatalogSearch(CatalogSnapshot catalog) =>
        candidates =
        [
            .. catalog.Champions.Entries.Select(static champion => Candidate.Of(
                ResourceType.Champions, champion.Summary.Id, champion.Summary.Name, true)),
            .. catalog.ListedItems.Select(static item => Candidate.Of(
                ResourceType.Items, item.Id, item.Name, false)),
            .. catalog.Runes.Entries.Select(static tree => Candidate.Of(
                ResourceType.Runes,
                tree.Id.ToString(CultureInfo.InvariantCulture),
                tree.Name,
                true)),
            .. catalog.Summoners.Entries.Select(static spell => Candidate.Of(
                ResourceType.Summoners, spell.Id, spell.Name, true)),
        ];

    /// <summary>The matches, resource by resource, at most <paramref name="limit"/> each.</summary>
    public IEnumerable<Candidate> Find(
        SearchQuery query,
        IReadOnlySet<ResourceType> types,
        int limit) =>
        candidates
            .Where(candidate => types.Contains(candidate.Type) && candidate.Matches(query))
            .GroupBy(static candidate => candidate.Type)
            .SelectMany(group => group.Take(limit));

    internal sealed record Candidate(
        ResourceType Type,
        string Id,
        string Name,
        string? FoldedId,
        string FoldedName)
    {
        public static Candidate Of(ResourceType type, string id, string name, bool byId) =>
            new(type, id, name, byId ? SearchQuery.Fold(id) : null, SearchQuery.Fold(name));

        public bool Matches(SearchQuery query) =>
            FoldedName.Contains(query.Needle, StringComparison.Ordinal)
            || (FoldedId?.Contains(query.Needle, StringComparison.Ordinal) ?? false);
    }
}
