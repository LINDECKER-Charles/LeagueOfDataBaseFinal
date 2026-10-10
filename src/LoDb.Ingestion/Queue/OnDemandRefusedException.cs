namespace LoDb.Ingestion.Queue;

/// <summary>
/// Synchronous on-demand work declined for now: the budget of cold work is spent, too many
/// ingestions already wait for a slot, or none freed in time.
/// </summary>
/// <remarks>
/// Transient, never persisted: a later call may be served. What was admitted before the
/// refusal still lands, so a caller reading the manifest afterwards may find part of it.
/// </remarks>
public sealed class OnDemandRefusedException : Exception
{
    public OnDemandRefusedException(string reason)
        : base($"On-demand ingestion refused ({reason}).")
    {
        Reason = reason;
    }

    /// <summary>
    /// The refusal, as <c>lodb_ingestion_on_demand_refused_total</c> counts it: dataset_budget,
    /// image_budget, backlog or slot_wait.
    /// </summary>
    public string Reason { get; }
}
