using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Fields;

namespace LoDb.Api.Modules.Catalog.States;

/// <summary>
/// A field of the block « Ce qui a changé dans ce patch », its values in the page's language
///: only the fields the index marks changed, each shown even when its two localized
/// values are equal.
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
    /// The values before; empty when the field is new, or when its localized values are equal
    /// (the value after shows alone).
    /// </summary>
    public required IReadOnlyList<ChangeValue> Before { get; init; }

    /// <summary>The values after; empty when the field is gone.</summary>
    public required IReadOnlyList<ChangeValue> After { get; init; }

    /// <summary>
    /// A text's diff, word by word (character by character in ja, zh, th), to write as text in
    /// <c>&lt;del&gt;</c> and <c>&lt;ins&gt;</c>, never as HTML; empty for another kind.
    /// </summary>
    public required IReadOnlyList<TextSegment> Segments { get; init; }
}
