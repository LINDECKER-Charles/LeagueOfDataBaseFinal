namespace LoDb.Domain.Builds.Structures;

/// <summary>A step of the purchase order as submitted or stored, read leniently.</summary>
public sealed record StepInput
{
    /// <summary>Null when absent or not a string.</summary>
    public string? Label { get; init; }

    /// <summary>
    /// The note when it is a string; null when absent, null or malformed, the last case
    /// flagged by <see cref="IsNoteMalformed"/>.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>Whether the note is neither null nor a string: a note may be left out.</summary>
    public bool IsNoteMalformed { get; init; }

    /// <summary>
    /// Null when absent or not a list; an entry is null when it is not a scalar, which no
    /// item id can be.
    /// </summary>
    public IReadOnlyList<string?>? Items { get; init; }
}
