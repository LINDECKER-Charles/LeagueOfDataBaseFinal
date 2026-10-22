namespace LoDb.Domain.States.Diff;

/// <summary>What a segment of a text diff is to the text before.</summary>
public enum SegmentKind
{
    /// <summary>In both texts.</summary>
    Same,

    /// <summary>In the text before only (<c>&lt;del&gt;</c>).</summary>
    Removed,

    /// <summary>In the text after only (<c>&lt;ins&gt;</c>).</summary>
    Added,
}
