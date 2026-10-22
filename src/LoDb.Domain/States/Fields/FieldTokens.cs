namespace LoDb.Domain.States.Fields;

/// <summary>
/// The closed tokens of the fields: the keys of the closed kinds of subject, and the one
/// value that is not a number of a <see cref="ValueKind.Range"/>.
/// </summary>
/// <remarks>
/// The other subjects are keyed by an id (<see cref="SubjectKind.Rune"/>,
/// <see cref="SubjectKind.Skin"/>), a <c>GameStat</c> in snake_case
/// (<see cref="SubjectKind.Stat"/>) or a <c>RuneSlotToken</c> (<see cref="SubjectKind.RuneSlot"/>).
/// </remarks>
public static class FieldTokens
{
    /// <summary>The value of a range that reaches the whole map (<c>RangeSentinels</c>).</summary>
    public const string GlobalRange = "global";

    /// <summary>The champion spells' slots, in the order Data Dragon lists the spells.</summary>
    public static IReadOnlyList<string> SpellSlots { get; } = ["q", "w", "e", "r"];

    /// <summary>Riot's ratings of a champion (<c>info</c>), in the page's order.</summary>
    public static IReadOnlyList<string> Ratings { get; } =
        ["attack", "defense", "magic", "difficulty"];
}
