namespace LoDb.Domain.Builds.Structures;

/// <summary>A step of the purchase order: a label, an optional note and item ids.</summary>
public sealed record BuildStep
{
    public required string Label { get; init; }

    /// <summary>Null when the step has none.</summary>
    public string? Note { get; init; }

    /// <summary>Data Dragon item ids in purchase order; the same id may repeat.</summary>
    public required IReadOnlyList<string> Items { get; init; }
}
