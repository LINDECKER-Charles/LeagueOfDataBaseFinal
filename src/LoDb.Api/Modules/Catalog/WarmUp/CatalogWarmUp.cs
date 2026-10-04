using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.WarmUp.Progress;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Queue;

namespace LoDb.Api.Modules.Catalog.WarmUp;

/// <summary>
/// The loader of a patch switch, for the long tail (ADR 0003): ingests the datasets of a
/// (version, language), then the images its lists show, and streams how far it got.
/// </summary>
/// <remarks>
/// It waits on the on-demand ingestion the pages use: a warm-up and a visit needing the same
/// image share one fetch, and the synchronous slots bound them together. A client leaving
/// stops the stream, never the ingestion, whose work stays for the next visit.
/// </remarks>
internal sealed class CatalogWarmUp(
    CatalogGateway gateway,
    IOnDemandIngestion onDemand,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Longest silence of the stream: the dataset ingestion of a cold version may outlast
    /// the client's watchdog, which the repeated last frame keeps waiting.
    /// </summary>
    public static readonly TimeSpan HeartbeatPeriod = TimeSpan.FromSeconds(5);

    private static readonly BoundedChannelOptions LatestOnly =
        new(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true };

    /// <summary>The frames of a warm-up, the last one done or failed.</summary>
    public async IAsyncEnumerable<WarmUpProgress> StreamAsync(
        WarmUpScope scope,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var frames = Channel.CreateBounded<WarmUpProgress>(LatestOnly);
        var warming = WarmAsync(scope, frames.Writer, cancellationToken);
        await foreach (var frame in ReadAsync(frames.Reader, cancellationToken))
        {
            yield return frame;
        }

        await warming;
    }

    // Never throws: an unexpected failure completes the frames, which hand it to the stream.
    private async Task WarmAsync(
        WarmUpScope scope,
        ChannelWriter<WarmUpProgress> frames,
        CancellationToken cancellationToken)
    {
        try
        {
            frames.TryWrite(WarmUpProgress.Preparing(scope.Resources));
            var read = await gateway.ReadAsync(scope.Catalog, cancellationToken);
            if (read.IsOpen)
            {
                var tracker = new WarmUpTracker(scope.Resources, frames);
                await WarmImagesAsync(read.Context, tracker, cancellationToken);
            }
            else
            {
                frames.TryWrite(WarmUpProgress.Failed(scope.Resources, read.Problem.Code));
            }

            frames.TryComplete();
        }
        catch (EgressException)
        {
            var code = CatalogProblem.UpstreamUnavailable().Code;
            frames.TryWrite(WarmUpProgress.Failed(scope.Resources, code));
            frames.TryComplete();
        }
        catch (Exception exception)
        {
            frames.TryComplete(exception);
        }
    }

    // One resource after the other, so that their rows turn ready in the order shown.
    private async Task WarmImagesAsync(
        CatalogContext context,
        WarmUpTracker tracker,
        CancellationToken cancellationToken)
    {
        var plan = WarmUpPlan.Of(context.Catalog, tracker.Resources);
        var stored = await context.ResolveAsync(
            plan.Select(static planned => planned.Image),
            ColdDemand.StoredOnly,
            cancellationToken);
        tracker.Start(
            [.. plan.Where(planned => stored.Of(planned.Image).Status == ImageStatus.Pending)]);
        foreach (var resource in tracker.Resources)
        {
            var images = tracker.PendingOf(resource);
            if (images.Count > 0)
            {
                var watched = new WatchedImages
                {
                    Version = context.Catalog.Version,
                    Images = images,
                    Progress = tracker,
                };
                await onDemand.EnsureImagesAsync(watched, cancellationToken);
            }

            tracker.Complete(resource);
        }

        tracker.Finish();
    }

    // The newest frame as soon as there is one, and the last one again after each silence
    // of a heartbeat period.
    private async IAsyncEnumerable<WarmUpProgress> ReadAsync(
        ChannelReader<WarmUpProgress> frames,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        WarmUpProgress? last = null;
        var next = frames.WaitToReadAsync(cancellationToken).AsTask();
        while (true)
        {
            if (!await ArrivesAsync(next, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (last is not null)
                {
                    yield return last;
                }

                continue;
            }

            if (!await next)
            {
                yield break;
            }

            while (frames.TryRead(out var frame))
            {
                last = frame;
            }

            if (last is not null)
            {
                yield return last;
            }

            next = frames.WaitToReadAsync(cancellationToken).AsTask();
        }
    }

    // Whether the frames answered within a heartbeat period; the timer is dropped either way.
    private async Task<bool> ArrivesAsync(Task<bool> next, CancellationToken cancellationToken)
    {
        using var beat = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var silence = Task.Delay(HeartbeatPeriod, timeProvider, beat.Token);
        var first = await Task.WhenAny(next, silence);
        await beat.CancelAsync();
        return first == next;
    }
}
