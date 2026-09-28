using LoDb.Ingestion.Queue;

namespace LoDb.Ingestion.Catalog;

/// <summary>
/// How a caller copes with cold data, and on whose behalf it reads.
/// </summary>
/// <param name="Policy">What the caller asks for.</param>
/// <param name="Origin">Who caused the read, which decides what it may cost.</param>
public sealed record ColdDemand(
    ColdPolicy Policy,
    OnDemandOrigin Origin = OnDemandOrigin.Visitor)
{
    public static ColdDemand Synchronous { get; } = new(ColdPolicy.Synchronous);

    public static ColdDemand Queued { get; } = new(ColdPolicy.Queued);

    public static ColdDemand StoredOnly { get; } = new(ColdPolicy.StoredOnly);

    /// <summary>
    /// The policy that applies: a crawler never waits for an ingestion, its synchronous reads
    /// are queued within its budget (C5).
    /// </summary>
    public ColdPolicy Effective =>
        Origin == OnDemandOrigin.Crawler && Policy == ColdPolicy.Synchronous
            ? ColdPolicy.Queued
            : Policy;

    /// <summary>The same policy, on behalf of <paramref name="origin"/>.</summary>
    public ColdDemand For(OnDemandOrigin origin) => this with { Origin = origin };
}
