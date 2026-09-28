using LoDb.Ingestion.Queue;

namespace LoDb.Api.Workers.Ingestion;

/// <summary>
/// Ingests the long-tail work queued by lists and previews (<see cref="IOnDemandIngestion"/>).
/// </summary>
internal sealed class OnDemandIngestionWorker(IOnDemandBacklog backlog) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        backlog.ConsumeAsync(stoppingToken);
}
