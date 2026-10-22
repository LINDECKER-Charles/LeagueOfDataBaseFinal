namespace LoDb.Domain.States.Fields;

/// <summary>
/// A field template: its dotted pattern, how its values compare and what they are, and the
/// kind of its subject, if it takes one.
/// </summary>
public sealed record FieldTemplate
{
    /// <summary>The segment of <see cref="Pattern"/> a subject's key fills.</summary>
    public const string SubjectSegment = "*";

    public required ChangeField Field { get; init; }

    /// <summary>
    /// Dotted segments, <see cref="SubjectSegment"/> standing for the subject: <c>name</c>,
    /// <c>stat.*</c>, <c>spell.*.cooldown</c>.
    /// </summary>
    public required string Pattern { get; init; }

    public required FieldKind Kind { get; init; }

    public required ValueKind Values { get; init; }

    /// <summary>The kind of its subject; null for a template without one.</summary>
    public SubjectKind? Subject { get; init; }
}
