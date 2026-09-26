namespace LoDb.Domain.Builds.Editing;

/// <summary>
/// Moves within a list, shared by the steps and the items of a step. A move that changes
/// nothing returns the list it was given.
/// </summary>
internal static class ListMoves
{
    public static IReadOnlyList<T> ByDelta<T>(IReadOnlyList<T> entries, int index, int delta)
    {
        var target = index + delta;
        if (delta == 0 || !IsIndex(entries, index) || !IsIndex(entries, target))
        {
            return entries;
        }

        return MoveTo(entries, index, target);
    }

    public static IReadOnlyList<T> ToInsertionPoint<T>(
        IReadOnlyList<T> entries,
        int from,
        int insertIndex)
    {
        if (!IsIndex(entries, from))
        {
            return entries;
        }

        var resting = RestingIndex(from, insertIndex, entries.Count);
        return resting == from ? entries : MoveTo(entries, from, resting);
    }

    /// <summary>
    /// Where an entry dropped at <paramref name="insertIndex"/> lands once it left
    /// <paramref name="from"/>: a drop after itself lands one place earlier.
    /// </summary>
    public static int RestingIndex(int from, int insertIndex, int length)
    {
        var target = ClampInsert(insertIndex, length);
        return target > from ? target - 1 : target;
    }

    public static int ClampInsert(int index, int length) => Math.Clamp(index, 0, length);

    public static bool IsIndex<T>(IReadOnlyList<T> entries, int index) =>
        index >= 0 && index < entries.Count;

    public static List<T> InsertAt<T>(IReadOnlyList<T> entries, int index, T entry)
    {
        List<T> next = [.. entries];
        next.Insert(ClampInsert(index, next.Count), entry);
        return next;
    }

    private static List<T> MoveTo<T>(IReadOnlyList<T> entries, int from, int to)
    {
        List<T> next = [.. entries];
        var entry = next[from];
        next.RemoveAt(from);
        next.Insert(to, entry);
        return next;
    }
}
