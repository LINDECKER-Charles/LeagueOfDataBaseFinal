namespace LoDb.Domain.States.Fields;

/// <summary>How the values of a field compare and show.</summary>
public enum FieldKind
{
    /// <summary>One value at most (none when the entry lacks it): before, then after.</summary>
    Value,

    /// <summary>Values whose order shows (a recipe, roles, per-rank numbers).</summary>
    List,

    /// <summary>
    /// Values without order, sorted ordinal (maps, tags, modes, skins): what was added and
    /// what was removed.
    /// </summary>
    Set,

    /// <summary>One plain text (<c>DdragonText.PlainText</c>), diffed word by word.</summary>
    Text,
}
