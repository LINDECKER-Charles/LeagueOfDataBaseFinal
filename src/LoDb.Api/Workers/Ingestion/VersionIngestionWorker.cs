using LoDb.Ingestion.Queue;

namespace LoDb.Api.Workers.Ingestion;

/// <summary>
/// Ingests the versions the patch watch queued on this instance, one at a time.
/// </summary>
internal sealed class VersionIngestionWorker(IVersionBacklog backlog) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        backlog.ConsumeAsync(stoppingToken);
}
