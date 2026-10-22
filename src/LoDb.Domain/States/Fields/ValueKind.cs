namespace LoDb.Domain.States.Fields;

/// <summary>
/// What a value of a field is, which tells the front how to show it. Every value travels as an
/// invariant string: OpenAPI refuses unions of numbers and strings.
/// </summary>
public enum ValueKind
{
    /// <summary>A number, <c>ToString("R", InvariantCulture)</c>; the front formats it.</summary>
    Number,

    /// <summary>A fraction to show as a percentage (0.25 is 25 %): an item's stat.</summary>
    Percent,

    /// <summary>
    /// Text shown as is: a name, a title, a resource, a per-rank string Data Dragon formats
    /// itself (<c>8/7/6/5/4</c>).
    /// </summary>
    Text,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Flag,

    /// <summary>
    /// A code the front translates with its table for the field (a mode, a map id, an item
    /// tag, a champion role), or shows raw when it has none.
    /// </summary>
    Code,

    /// <summary>
    /// The id of another entity or sub-entity (an item, a champion, a rune, a skin), which the
    /// API names in the page's language.
    /// </summary>
    Entity,

    /// <summary>A number, or <c>global</c> for a range that reaches the whole map.</summary>
    Range,
}
