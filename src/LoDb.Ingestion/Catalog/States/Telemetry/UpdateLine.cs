using LoDb.Ingestion.Catalog.States.Storage;

namespace LoDb.Ingestion.Catalog.States.Telemetry;

/// <summary>What the summary line of an update says.</summary>
internal sealed record UpdateLine
{
    /// <summary>The trigger, as its metric tag (<c>build</c>, <c>promotion</c>…).</summary>
    public required string Trigger { get; init; }

    /// <summary>The outcome, as its metric tag (<c>unchanged</c>, <c>built</c>…).</summary>
    public required string Outcome { get; init; }

    /// <summary>The current generation after the update; null while none is.</summary>
    public GenerationSummary? Summary { get; init; }

    public int Versions { get; init; }

    /// <summary>The versions whose en_US datasets the update ingested first.</summary>
    public int Ingested { get; init; }

    public int Opened { get; init; }

    public int Closed { get; init; }

    public long DurationMs { get; init; }
}
