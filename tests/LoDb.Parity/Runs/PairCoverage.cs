namespace LoDb.Parity.Runs;

/// <summary>
/// The languages the champion datasets of one (version, language) are written in, on each
/// side: a language Data Dragon lacks for a version falls back to en_US (N/A in the report).
/// </summary>
public sealed record PairCoverage
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    public string? Legacy { get; init; }

    public string? Next { get; init; }

    public bool IsFallback => Legacy != Language || Next != Language;
}
