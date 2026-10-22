namespace LoDb.Api.Modules.Catalog.States;

/// <summary>
/// A value of a changed field, as an invariant string, and its display name when it
/// is the id of an entity or a sub-entity.
/// </summary>
internal sealed record ChangeValue
{
    /// <summary>
    /// A number, a text, a code, an id, <c>true</c> or <c>false</c>, as its <c>valueKind</c> says.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// The name, in the page's language, of the item, champion, rune or skin
    /// <see cref="Value"/> identifies; null for any other kind of value.
    /// </summary>
    public required string? Label { get; init; }
}
