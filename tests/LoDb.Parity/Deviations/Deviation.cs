namespace LoDb.Parity.Deviations;

/// <summary>One difference between the legacy and the new stack.</summary>
public sealed record Deviation
{
    private static readonly IReadOnlySet<string> NoTags = new HashSet<string>();

    public required DeviationSite Site { get; init; }

    public required DeviationKind Kind { get; init; }

    /// <summary>The path inside the entry, such as <c>spells[AatroxQ].image</c>.</summary>
    public string Field { get; init; } = "";

    /// <summary>The legacy value, rendered; null when the legacy stack has none.</summary>
    public string? Legacy { get; init; }

    /// <summary>The new value, rendered; null when the new stack has none.</summary>
    public string? Next { get; init; }

    /// <summary>
    /// Facts about the entry or key the rules classify on (<see cref="DeviationTags"/>):
    /// a deviation alone does not say whether its item is debris or its icon an ability.
    /// </summary>
    public IReadOnlySet<string> Tags { get; init; } = NoTags;
}
