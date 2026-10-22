using LoDb.Domain.States.Fields;

namespace LoDb.Domain.States.Diff;

/// <summary>
/// A field the index marks changed, with its values in the page's language: what the block
/// « Ce qui a changé » shows of it.
/// </summary>
/// <remarks>
/// <see cref="Before"/> and <see cref="After"/> are the projected values
/// (<c>ProjectedField.Values</c>). When the two localized values are equal (a translation
/// that lags, a fallback to en_US), <see cref="Before"/> is empty and the field shows its value
/// after alone. A text field also carries its <see cref="Segments"/>, empty otherwise.
/// </remarks>
public sealed record FieldDiff
{
    public required FieldKey Key { get; init; }

    /// <summary>
    /// The subject's name in the page's language (a spell's, a rune's, a skin's), from the
    /// projection after, else before; null for a template without a named subject.
    /// </summary>
    public string? SubjectName { get; init; }

    public required IReadOnlyList<string> Before { get; init; }

    public required IReadOnlyList<string> After { get; init; }

    public required IReadOnlyList<TextSegment> Segments { get; init; }
}
