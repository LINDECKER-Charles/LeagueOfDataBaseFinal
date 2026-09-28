namespace LoDb.Ingestion.Queue;

/// <summary>Who caused an on-demand request, which decides what it may cost.</summary>
public enum OnDemandOrigin
{
    /// <summary>A visitor or an app: served synchronously when the context allows it.</summary>
    Visitor,

    /// <summary>
    /// A crawler: never waits for an ingestion, and queues within a budget per minute (C5).
    /// </summary>
    Crawler,
}
