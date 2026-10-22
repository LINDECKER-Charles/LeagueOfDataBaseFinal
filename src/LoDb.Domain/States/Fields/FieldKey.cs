using System.Diagnostics.CodeAnalysis;
using LoDb.Domain.Paths;

namespace LoDb.Domain.States.Fields;

/// <summary>
/// A field of an entity: a template and, when the template takes one, its subject's key
/// (<c>spell.q.cooldown</c> is <see cref="ChangeField.SpellCooldown"/> of the Q spell).
/// </summary>
/// <remarks>
/// The dotted form is what <c>catalog_entity_state.changed_fields</c> stores: the
/// template's pattern, its <see cref="FieldTemplate.SubjectSegment"/> replaced by the subject.
/// A subject is one segment of ASCII letters, digits and underscores, so a key never holds a
/// stray dot and parses back to one template only.
/// </remarks>
public sealed record FieldKey
{
    private const char Separator = '.';

    private FieldKey(ChangeField field, string? subject)
    {
        Field = field;
        Subject = subject;
    }

    public ChangeField Field { get; }

    /// <summary>The subject's key; null for a template without a subject.</summary>
    public string? Subject { get; }

    /// <summary>The field of a template without a subject.</summary>
    public static FieldKey Of(ChangeField field) =>
        ChangeFields.Of(field).Subject is null
            ? new FieldKey(field, null)
            : throw new ArgumentException($"{field} takes a subject.", nameof(field));

    /// <summary>The field of <paramref name="subject"/> for a template with a subject.</summary>
    public static FieldKey Of(ChangeField field, string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (ChangeFields.Of(field).Subject is null)
        {
            throw new ArgumentException($"{field} takes no subject.", nameof(field));
        }

        return EntityIds.IsWord(subject)
            ? new FieldKey(field, subject)
            : throw new ArgumentException($"'{subject}' cannot be a subject.", nameof(subject));
    }

    /// <summary>The dotted form: <c>name</c>, <c>stat.health</c>, <c>spell.q.name</c>.</summary>
    public string ToDotted()
    {
        var pattern = ChangeFields.Of(Field).Pattern;
        return Subject is null
            ? pattern
            : pattern.Replace(FieldTemplate.SubjectSegment, Subject, StringComparison.Ordinal);
    }

    public static bool TryParse(
        [NotNullWhen(true)] string? dotted,
        [NotNullWhen(true)] out FieldKey? key)
    {
        key = dotted is null ? null : Exact(dotted) ?? WithSubject(dotted.Split(Separator));
        return key is not null;
    }

    public static FieldKey Parse(string dotted) =>
        TryParse(dotted, out var key)
            ? key
            : throw new FormatException($"'{dotted}' is not the key of a field.");

    public override string ToString() => ToDotted();

    private static FieldKey? Exact(string dotted) =>
        ChangeFields.WithPattern(dotted) is { Subject: null } template
            ? new FieldKey(template.Field, null)
            : null;

    // Tries each segment as the subject: the pattern of at most one template matches.
    private static FieldKey? WithSubject(string[] segments)
    {
        for (var index = 0; index < segments.Length; index++)
        {
            var subject = segments[index];
            if (!EntityIds.IsWord(subject))
            {
                continue;
            }

            segments[index] = FieldTemplate.SubjectSegment;
            var template = ChangeFields.WithPattern(string.Join(Separator, segments));
            segments[index] = subject;
            if (template?.Subject is not null)
            {
                return new FieldKey(template.Field, subject);
            }
        }

        return null;
    }
}
