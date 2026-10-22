namespace LoDb.Ingestion.Pipeline.Versions;

/// <summary>
/// When a promotion listener runs: the stages run in this order, whatever the order the
/// modules registered their listeners in. Listeners of one stage keep that order.
/// </summary>
/// <remarks>
/// The later stages read what the promotion changed from the index of states: they run once
/// the index covers the promoted version, or log and stop.
/// </remarks>
public enum PromotionStage
{
    /// <summary>Extends the index of states to the promoted version.</summary>
    StateIndex = 0,

    /// <summary>Queues the pages to store again: the version that left the latest rank.</summary>
    Prerender = 1,

    /// <summary>Tells search engines what changed (IndexNow).</summary>
    Announce = 2,
}
