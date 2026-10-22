namespace LoDb.Domain.States.Diff;

/// <summary>
/// A run of tokens of a text diff, as plain text: the front writes it as text, never as HTML
/// (Trusted Types, and Riot's bare <c>&lt;li&gt;</c> break the hydration).
/// </summary>
public sealed record TextSegment
{
    public required SegmentKind Kind { get; init; }

    public required string Text { get; init; }
}
