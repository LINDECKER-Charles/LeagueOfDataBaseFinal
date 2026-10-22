namespace LoDb.Domain.States.Diff.Tokens;

/// <summary>
/// The alignment of two token lists by their longest common subsequence: the tokens of both,
/// those of the text before only, those of the text after only.
/// </summary>
/// <remarks>
/// The common head and tail are set aside first, so the quadratic table only spans what
/// changed. Beyond <see cref="MaxCells"/> (two long texts rewritten through), the middle
/// shows as removed then added: a coarser diff, never a costly one.
/// </remarks>
internal static class TokenAlignment
{
    private const long MaxCells = 1_000_000;

    public static IReadOnlyList<TextSegment> Of(
        IReadOnlyList<string> before,
        IReadOnlyList<string> after)
    {
        var head = CommonHead(before, after);
        var tail = CommonTail(before, after, head);
        var runs = new SegmentRuns();
        runs.AddAll(SegmentKind.Same, before.Take(head));
        Middle(
            runs,
            [.. before.Skip(head).Take(before.Count - head - tail)],
            [.. after.Skip(head).Take(after.Count - head - tail)]);
        runs.AddAll(SegmentKind.Same, before.Skip(before.Count - tail));
        return runs.ToList();
    }

    private static void Middle(SegmentRuns runs, string[] before, string[] after)
    {
        if ((long)(before.Length + 1) * (after.Length + 1) > MaxCells)
        {
            runs.AddAll(SegmentKind.Removed, before);
            runs.AddAll(SegmentKind.Added, after);
            return;
        }

        var lengths = CommonLengths(before, after);
        var width = after.Length + 1;
        var (row, column) = (0, 0);
        while (row < before.Length && column < after.Length)
        {
            if (string.Equals(before[row], after[column], StringComparison.Ordinal))
            {
                runs.Add(SegmentKind.Same, before[row++]);
                column++;
            }
            else if (lengths[((row + 1) * width) + column] >= lengths[(row * width) + column + 1])
            {
                runs.Add(SegmentKind.Removed, before[row++]);
            }
            else
            {
                runs.Add(SegmentKind.Added, after[column++]);
            }
        }

        runs.AddAll(SegmentKind.Removed, before.Skip(row));
        runs.AddAll(SegmentKind.Added, after.Skip(column));
    }

    // lengths[row, column]: the longest common subsequence of the suffixes from there, flat.
    private static int[] CommonLengths(string[] before, string[] after)
    {
        var width = after.Length + 1;
        var lengths = new int[(before.Length + 1) * width];
        for (var row = before.Length - 1; row >= 0; row--)
        {
            for (var column = after.Length - 1; column >= 0; column--)
            {
                lengths[(row * width) + column] =
                    string.Equals(before[row], after[column], StringComparison.Ordinal)
                        ? lengths[((row + 1) * width) + column + 1] + 1
                        : Math.Max(
                            lengths[((row + 1) * width) + column],
                            lengths[(row * width) + column + 1]);
            }
        }

        return lengths;
    }

    private static int CommonHead(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var head = 0;
        while (head < before.Count
            && head < after.Count
            && string.Equals(before[head], after[head], StringComparison.Ordinal))
        {
            head++;
        }

        return head;
    }

    private static int CommonTail(
        IReadOnlyList<string> before,
        IReadOnlyList<string> after,
        int head)
    {
        var tail = 0;
        while (tail < before.Count - head
            && tail < after.Count - head
            && string.Equals(
                before[before.Count - 1 - tail],
                after[after.Count - 1 - tail],
                StringComparison.Ordinal))
        {
            tail++;
        }

        return tail;
    }
}
