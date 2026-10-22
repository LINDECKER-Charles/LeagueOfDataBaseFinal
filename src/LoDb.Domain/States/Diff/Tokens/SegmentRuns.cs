namespace LoDb.Domain.States.Diff.Tokens;

/// <summary>
/// The segments of a diff as its tokens come: consecutive tokens of one kind join into one
/// segment.
/// </summary>
internal sealed class SegmentRuns
{
    private readonly List<TextSegment> segments = [];

    public void Add(SegmentKind kind, string token)
    {
        if (token.Length == 0)
        {
            return;
        }

        if (segments.Count > 0 && segments[^1].Kind == kind)
        {
            segments[^1] = segments[^1] with { Text = segments[^1].Text + token };
            return;
        }

        segments.Add(new TextSegment { Kind = kind, Text = token });
    }

    public void AddAll(SegmentKind kind, IEnumerable<string> tokens)
    {
        foreach (var token in tokens)
        {
            Add(kind, token);
        }
    }

    public IReadOnlyList<TextSegment> ToList() => [.. segments];
}
