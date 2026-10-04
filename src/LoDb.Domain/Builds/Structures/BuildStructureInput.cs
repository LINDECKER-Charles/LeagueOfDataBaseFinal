namespace LoDb.Domain.Builds.Structures;

/// <summary>
/// A build structure as submitted or stored, read leniently: each member keeps what could be
/// read, null standing for a value absent or of the wrong shape, which the rules then report.
/// </summary>
public sealed record BuildStructureInput
{
    /// <summary>Null when absent or not a string.</summary>
    public string? ChampionId { get; init; }

    /// <summary>Null when absent or not an object.</summary>
    public RunePageInput? Runes { get; init; }

    /// <summary>
    /// Null when absent or not a list; an entry is null when it is not an object.
    /// </summary>
    public IReadOnlyList<StepInput?>? Steps { get; init; }
}
