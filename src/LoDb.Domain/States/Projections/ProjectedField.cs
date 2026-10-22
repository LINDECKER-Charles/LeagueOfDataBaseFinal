using LoDb.Domain.States.Fields;

namespace LoDb.Domain.States.Projections;

/// <summary>
/// A field of an entry's displayed projection: what its page shows of the entity
/// itself, as invariant strings.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Values"/> hold, by the template's <see cref="FieldKind"/>: one value or none
/// (<see cref="FieldKind.Value"/>, none when the entry lacks it); the values in the page's
/// order (<see cref="FieldKind.List"/>); the values sorted ordinal, each once
/// (<see cref="FieldKind.Set"/>); one text in <c>DdragonText.PlainText</c> form
/// (<see cref="FieldKind.Text"/>). Numbers are <c>ToString("R", InvariantCulture)</c>.
/// </para>
/// <para>
/// The projection of the en_US entry gives the fingerprint and the index's changed keys; that
/// of the page's language gives the values the block « Ce qui a changé » shows.
/// </para>
/// </remarks>
public sealed record ProjectedField
{
    public required FieldKey Key { get; init; }

    public required IReadOnlyList<string> Values { get; init; }
}
