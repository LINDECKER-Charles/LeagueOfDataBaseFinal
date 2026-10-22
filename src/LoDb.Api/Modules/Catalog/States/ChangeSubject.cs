using LoDb.Domain.States.Fields;

namespace LoDb.Api.Modules.Catalog.States;

/// <summary>
/// The sub-entity a changed field belongs to: a spell slot, a stat, a rating, a rune, a row of
/// runes or a skin.
/// </summary>
internal sealed record ChangeSubject
{
    public required SubjectKind Kind { get; init; }

    /// <summary>
    /// <c>q</c>, a stat in snake_case (the key of its <c>stat.*</c> label), a rating, a rune or
    /// skin id, a rune row token (<c>keystone</c>, <c>row1</c>…).
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Its name in the page's language (a spell's, a rune's, a skin's); null where the front
    /// labels the key itself (stats, ratings, rows), or where the answer reads no catalog (the
    /// history).
    /// </summary>
    public required string? Name { get; init; }
}
