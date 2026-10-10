using System.Diagnostics.CodeAnalysis;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>
/// The catalogs in memory, in two tiers: the pinned ones, never evicted, and the others, the
/// least recently read evicted first once <c>LoDb:Catalog:MaxEntries</c> are held.
/// </summary>
/// <remarks>
/// A sweep over many (version, language) only ever cycles the second tier: the catalogs of
/// the latest version, which most pages read, stay. Not thread-safe: the cache guards it.
/// </remarks>
/// <param name="capacity">Places of the second tier.</param>
/// <param name="evicted">Told of each catalog the second tier lets go.</param>
internal sealed class HeldCatalogs(int capacity, Action evicted)
{
    private readonly Dictionary<CatalogKey, CatalogSnapshot> pinned = [];
    private readonly Dictionary<CatalogKey, LinkedListNode<Held>> recent = [];
    private readonly LinkedList<Held> recency = new();
    private HashSet<CatalogKey> pinnedKeys = [];

    /// <summary>The keys of the second tier, the most recently read first.</summary>
    public IReadOnlyList<CatalogKey> RecentKeys => [.. recency.Select(static held => held.Key)];

    /// <summary>The keys of the pinned catalogs held.</summary>
    public IReadOnlyCollection<CatalogKey> PinnedKeys => [.. pinned.Keys];

    /// <summary>The catalog held under the key, which becomes the most recently read.</summary>
    public bool TryGet(CatalogKey key, [NotNullWhen(true)] out CatalogSnapshot? catalog)
    {
        if (pinned.TryGetValue(key, out catalog))
        {
            return true;
        }

        if (recent.TryGetValue(key, out var node))
        {
            Touch(node);
            catalog = node.Value.Catalog;
            return true;
        }

        return false;
    }

    /// <summary>Holds a catalog just loaded, in the tier its key belongs to.</summary>
    public void Hold(CatalogKey key, CatalogSnapshot catalog)
    {
        if (pinnedKeys.Contains(key))
        {
            pinned[key] = catalog;
        }
        else
        {
            Remember(key, catalog);
        }
    }

    /// <summary>
    /// Makes <paramref name="keys"/> the pinned ones: those already held move to the pinned
    /// tier, the pinned ones left out move back to the second tier, as just read.
    /// </summary>
    public void Pin(IReadOnlyCollection<CatalogKey> keys)
    {
        pinnedKeys = [.. keys];
        List<KeyValuePair<CatalogKey, CatalogSnapshot>> released =
            [.. pinned.Where(entry => !pinnedKeys.Contains(entry.Key))];
        foreach (var (key, catalog) in released)
        {
            pinned.Remove(key);
            Remember(key, catalog);
        }

        foreach (var key in pinnedKeys)
        {
            if (recent.Remove(key, out var node))
            {
                recency.Remove(node);
                pinned[key] = node.Value.Catalog;
            }
        }
    }

    private void Remember(CatalogKey key, CatalogSnapshot catalog)
    {
        if (recent.Remove(key, out var previous))
        {
            recency.Remove(previous);
        }

        recent[key] = recency.AddFirst(new Held(key, catalog));
        while (recent.Count > capacity && recency.Last is { } oldest)
        {
            recency.RemoveLast();
            recent.Remove(oldest.Value.Key);
            evicted();
        }
    }

    private void Touch(LinkedListNode<Held> node)
    {
        recency.Remove(node);
        recency.AddFirst(node);
    }

    private sealed record Held(CatalogKey Key, CatalogSnapshot Catalog);
}
