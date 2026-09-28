using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Pipeline.Watch;

/// <summary>
/// Discovers the versions newer than every known one, then queues the due versions, newest
/// first.
/// </summary>
/// <remarks>
/// A database that knows no version starts from the newest one alone: the older versions are
/// the long tail, ingested on demand or from the command line, never four hundred full
/// ingestions at the first start.
/// </remarks>
internal sealed class PatchWatch(
    KnownReleases releases,
    IDdragonVersionStore store,
    VersionStates states,
    IVersionBacklog backlog,
    ILogger<PatchWatch> logger) : IPatchWatch
{
    public async Task<int> WatchAsync(CancellationToken cancellationToken)
    {
        var upstream = await releases.RefreshAsync(cancellationToken).ConfigureAwait(false);
        await DiscoverAsync(upstream, cancellationToken).ConfigureAwait(false);
        var due = await states.DueAsync(cancellationToken).ConfigureAwait(false);
        var queued = 0;
        foreach (var version in due)
        {
            if (backlog.TryEnqueue(version))
            {
                queued++;
            }
        }

        return queued;
    }

    private async Task DiscoverAsync(
        IReadOnlyList<PatchVersion> upstream,
        CancellationToken cancellationToken)
    {
        var known = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        var newest = known.Select(static row => PatchVersion.Parse(row.Version)).Max();
        var fresh = newest is null
            ? upstream.Take(1)
            : upstream.Where(version => version > newest);
        foreach (var version in fresh)
        {
            if (await store.DiscoverAsync(version.Value, cancellationToken).ConfigureAwait(false))
            {
                IngestionLog.VersionDiscovered(logger, version.Value);
            }
        }
    }
}
