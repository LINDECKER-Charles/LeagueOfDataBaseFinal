using LoDb.Parity.Deviations;

namespace LoDb.Parity.Classification;

/// <summary>A class given to every deviation that matches, with its justification.</summary>
public sealed record DeviationRule
{
    /// <summary>Stable name, quoted by the parity report.</summary>
    public required string Id { get; init; }

    public required DeviationClass Class { get; init; }

    public required string Justification { get; init; }

    public required Func<Deviation, bool> Matches { get; init; }
}
