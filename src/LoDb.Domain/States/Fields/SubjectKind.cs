namespace LoDb.Domain.States.Fields;

/// <summary>
/// What the subject of a field template designates; its key's shape is in
/// <see cref="FieldTokens"/>.
/// </summary>
public enum SubjectKind
{
    /// <summary>A champion spell by its slot: <c>q</c>, <c>w</c>, <c>e</c>, <c>r</c>.</summary>
    SpellSlot,

    /// <summary>
    /// A stat by its <c>GameStat</c> in snake_case (<c>attack_damage</c>), the key of its
    /// existing <c>stat.*</c> label.
    /// </summary>
    Stat,

    /// <summary>
    /// A rating of Riot's: <c>attack</c>, <c>defense</c>, <c>magic</c>, <c>difficulty</c>.
    /// </summary>
    Rating,

    /// <summary>A rune of a path by its id (<c>8005</c>).</summary>
    Rune,

    /// <summary>A row of a rune path: <c>keystone</c>, <c>row1</c> to <c>row3</c>.</summary>
    RuneSlot,

    /// <summary>A skin by its id (<c>103001</c>).</summary>
    Skin,
}
