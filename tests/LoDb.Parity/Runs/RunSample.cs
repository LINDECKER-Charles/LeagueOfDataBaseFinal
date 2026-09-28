namespace LoDb.Parity.Runs;

/// <summary><c>sample.json</c> of a run: what tools/next/parity collected.</summary>
public sealed record RunSample
{
    public required IReadOnlyList<string> Versions { get; init; }

    public required IReadOnlyList<string> Languages { get; init; }

    public string? CollectedAt { get; init; }
}
