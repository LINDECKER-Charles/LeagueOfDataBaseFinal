using LoDb.Infrastructure.Jobs;
using LoDb.Ingestion.Pipeline;
using LoDb.Ingestion.Pipeline.Watch;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Workers.Ingestion;

/// <summary>
/// The patch watch (ADR 0003), every <c>LoDb:Ingestion:WatchPeriod</c> on one instance at a
/// time: new versions are discovered and the due ones queued for the ingestion worker.
/// </summary>
internal sealed class PatchWatchJob(
    PeriodicJobServices services,
    IPatchWatch watch,
    IOptions<IngestionOptions> options) : PeriodicJob(services)
{
    public override string Name => "ingestion.patch-watch";

    public override TimeSpan Period => options.Value.WatchPeriod;

    // Counts the versions queued: the discovered ones and those due for another attempt.
    protected override async Task<JobRunSummary> RunOnceAsync(
        CancellationToken cancellationToken) =>
        new(await watch.WatchAsync(cancellationToken), "versions");
}
