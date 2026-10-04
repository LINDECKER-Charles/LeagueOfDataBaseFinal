using System.Collections.Frozen;
using LoDb.Domain.Languages;
using LoDb.Ingestion.Normalization;

namespace LoDb.Ingestion.Catalog.Snapshots;

/// <summary>
/// The entries of one resource in the upstream order, indexed by id.
/// </summary>
/// <typeparam name="TEntry">The domain entry.</typeparam>
public sealed class CatalogResource<TEntry>
    where TEntry : class
{
    private readonly FrozenDictionary<string, TEntry> byId;

    internal CatalogResource(DatasetDocument<TEntry> document, Func<TEntry, string> idOf)
    {
        Entries = document.Entries;
        ContentLanguage = DdragonLanguage.TryParse(document.ContentLanguage, out var content)
            ? content
            : null;

        // The first entry of an id wins, as in the id-keyed dataset map.
        var index = new Dictionary<string, TEntry>(StringComparer.Ordinal);
        foreach (var entry in document.Entries)
        {
            index.TryAdd(idOf(entry), entry);
        }

        byId = index.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Every entry, in the upstream order.</summary>
    public IReadOnlyList<TEntry> Entries { get; }

    /// <summary>
    /// The language the entries are written in: the catalog's, en_US after a fallback (UP 2),
    /// or <see langword="null"/> when no language ships the dataset, which is then empty.
    /// </summary>
    public DdragonLanguage? ContentLanguage { get; }

    /// <summary>The entry of an id, compared ordinally as the dataset keys it.</summary>
    public TEntry? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return byId.GetValueOrDefault(id);
    }
}
