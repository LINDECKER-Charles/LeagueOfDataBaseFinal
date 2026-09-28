using System.Collections.Concurrent;
using System.Threading.Channels;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Pipeline;
using LoDb.Ingestion.Pipeline.Versions;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Queue;

/// <summary>
/// A bounded channel of versions, without duplicates, ingested one after the other.
/// </summary>
/// <remarks>
/// One version at a time: each run is already parallel inside, and a second one would only
/// share the same egress. A version stays counted until its run ends, so that the watch does
/// not queue it again meanwhile.
/// </remarks>
internal sealed class VersionBacklog : IVersionBacklog
{
    public const string QueueName = "versions";

    private const int Capacity = 32;

    private readonly Channel<PatchVersion> channel = Channel.CreateBounded<PatchVersion>(
        new BoundedChannelOptions(Capacity)
        {
            // Wait, not a drop mode: TryWrite then fails when the channel is full, where
            // the drop modes would report the write as done.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    private readonly ConcurrentDictionary<PatchVersion, byte> pending = new();
    private readonly IVersionIngestion ingestion;
    private readonly ILogger<VersionBacklog> logger;

    public VersionBacklog(
        IVersionIngestion ingestion,
        IngestionMetrics metrics,
        ILogger<VersionBacklog> logger)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        this.ingestion = ingestion;
        this.logger = logger;
        metrics.ObserveQueue(QueueName, () => pending.Count);
    }

    public int Count => pending.Count;

    public bool TryEnqueue(PatchVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (!pending.TryAdd(version, 0))
        {
            return true;
        }

        if (channel.Writer.TryWrite(version))
        {
            return true;
        }

        pending.TryRemove(version, out _);
        return false;
    }

    public async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        await foreach (var version in channel.Reader.ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            try
            {
                await ingestion.IngestAsync(version, IngestionRequest.Complete, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (!IsStopping(exception, cancellationToken))
            {
                IngestionLog.VersionFailed(logger, version.Value, exception);
            }
            finally
            {
                pending.TryRemove(version, out _);
            }
        }
    }

    private static bool IsStopping(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
}
