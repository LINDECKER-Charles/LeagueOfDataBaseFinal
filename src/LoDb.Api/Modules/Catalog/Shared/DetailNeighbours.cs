namespace LoDb.Api.Modules.Catalog.Shared;

/// <summary>
/// The entries on either side of a detail page in its list's order, without wrapping round:
/// the first entry has no previous one. Sent with the page, so that the pager is in the
/// server's HTML rather than filled in after the browser fetched the whole list.
/// </summary>
internal sealed record DetailNeighbours
{
    /// <summary>The neighbours of an entry the list does not hold.</summary>
    public static DetailNeighbours None { get; } = new() { Previous = null, Next = null };

    public required DetailNeighbour? Previous { get; init; }

    public required DetailNeighbour? Next { get; init; }

    /// <param name="ordered">
    /// The list in the order its endpoint answers it, so that the pager cannot drift from it.
    /// </param>
    /// <param name="isCurrent">
    /// Whether an entry is the page's, by id: the first entry of an id is the one its page
    /// shows, as in the catalog's index.
    /// </param>
    /// <param name="link">An entry as the pager links it.</param>
    public static DetailNeighbours Around<TEntry>(
        IReadOnlyList<TEntry> ordered,
        Func<TEntry, bool> isCurrent,
        Func<TEntry, DetailNeighbour> link)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        ArgumentNullException.ThrowIfNull(isCurrent);
        ArgumentNullException.ThrowIfNull(link);
        var index = IndexOf(ordered, isCurrent);
        if (index < 0)
        {
            return None;
        }

        return new DetailNeighbours
        {
            Previous = index > 0 ? link(ordered[index - 1]) : null,
            Next = index < ordered.Count - 1 ? link(ordered[index + 1]) : null,
        };
    }

    private static int IndexOf<TEntry>(IReadOnlyList<TEntry> ordered, Func<TEntry, bool> isCurrent)
    {
        for (var index = 0; index < ordered.Count; index++)
        {
            if (isCurrent(ordered[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
