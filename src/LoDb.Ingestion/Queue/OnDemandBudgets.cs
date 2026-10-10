using LoDb.Ingestion.Pipeline;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Queue;

/// <summary>
/// The per-minute budgets of the on-demand ingestion on this instance (ADR 0003, § 5).
/// </summary>
/// <remarks>
/// A version already ingested costs nothing and needs none: only work that reaches Data
/// Dragon is counted, and a caller joining work in flight takes nothing.
/// </remarks>
internal sealed class OnDemandBudgets(IOptions<IngestionOptions> options, TimeProvider timeProvider)
{
    /// <summary>Requests a crawler may queue (<c>CrawlerRequestsPerMinute</c>, C5).</summary>
    public MinuteBudget Crawler { get; } =
        new(options.Value.CrawlerRequestsPerMinute, timeProvider);

    /// <summary>
    /// (version, language) pairs a visitor's read may ingest while it waits
    /// (<c>ColdDatasetsPerMinute</c>).
    /// </summary>
    public MinuteBudget Datasets { get; } = new(options.Value.ColdDatasetsPerMinute, timeProvider);

    /// <summary>
    /// Images a visitor's read may fetch while it waits (<c>ColdImagesPerMinute</c>).
    /// </summary>
    public MinuteBudget Images { get; } = new(options.Value.ColdImagesPerMinute, timeProvider);
}
