using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Fields;

namespace LoDb.Api.Modules.Catalog.States;

/// <summary>
/// A field of the block « Ce qui a changé dans ce patch », its values in the page's language:
/// exactly the fields the index marks changed, each shown even when its two localized values
/// are equal, or when neither side has one (no values, no segments).
/// </summary>
internal sealed record FieldChange
{
    public required ChangeField Field { get; init; }

    public required ChangeSubject? Subject { get; init; }

    /// <summary>
    /// How to show the values: one before and after, a list in order, a set (what was added
    /// and removed), or a text by its segments.
    /// </summary>
    public required FieldKind Kind { get; init; }

    public required ValueKind ValueKind { get; init; }

    /// <summary>
    /// The values before; empty when the field is new, when its localized values are equal or
    /// the version compared had it only in en_US (the value after shows alone), and for a
    /// text, which its segments carry.
    /// </summary>
    public required IReadOnlyList<ChangeValue> Before { get; init; }

    /// <summary>The values after; empty when the field is gone, and for a text.</summary>
    public required IReadOnlyList<ChangeValue> After { get; init; }

    /// <summary>
    /// A text's diff, word by word (character by character in ja, zh, th), to write as text in
    /// <c>&lt;del&gt;</c> and <c>&lt;ins&gt;</c>, never as HTML; one <c>same</c> segment when
    /// its localized texts are equal or the version compared had it only in en_US; empty for
    /// another kind, and for a text neither side has in the page's language.
    /// </summary>
    public required IReadOnlyList<TextSegment> Segments { get; init; }
}
