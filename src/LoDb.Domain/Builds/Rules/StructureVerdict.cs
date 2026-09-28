namespace LoDb.Domain.Builds.Rules;

/// <summary>What the rules found wrong with a structure, empty when it may be saved.</summary>
public sealed record StructureVerdict
{
    /// <summary>The codes of <see cref="BuildErrors"/>, each once.</summary>
    public required IReadOnlyList<string> Codes { get; init; }

    /// <summary>
    /// The names <see cref="BuildErrors.ItemMode"/> lists: items of the patch the mode's map
    /// does not offer, classic ones qualified by their id.
    /// </summary>
    public required IReadOnlyList<string> UnavailableItems { get; init; }

    public bool IsValid => Codes.Count == 0;
}
