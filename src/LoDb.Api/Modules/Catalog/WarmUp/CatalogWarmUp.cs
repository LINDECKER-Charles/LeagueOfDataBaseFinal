using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.WarmUp.Limits;
using LoDb.Api.Modules.Catalog.WarmUp.Progress;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Catalog.WarmUp;

/// <summary>
/// The loader of a patch switch, for the long tail (ADR 0003): ingests the datasets of a
/// (version, language), then the images its lists show, and streams how far it got.
/// </summary>
/// <remarks>
/// <para>
/// It waits on the on-demand ingestion the pages use: a warm-up and a visit needing the same
/// image share one fetch, the synchronous slots and the budget of cold work bound them
/// together. A client leaving stops the stream, never the ingestion, whose work stays for the
/// next visit.
/// </para>
/// <para>
/// Image work refused for now (budget spent, no slot) asks for no more images: the run ends
/// done, the catalog being open, and the images left stay pending for the pages, which show
/// them as they arrive. A catalog that cannot be opened (refused as well, unknown, Data Dragon
/// down) ends the run with a failed frame, as does a stream past
/// <c>LoDb:Catalog:WarmUp:StreamLifetime</c>; the client visits the page all the same.
/// </para>
/// </remarks>
internal sealed class CatalogWarmUp(
    CatalogGateway gateway,
    IOnDemandIngestion onDemand,
    IOptions<WarmUpOptions> options,
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
        using var expiry = new CancellationTokenSource(options.Value.StreamLifetime, timeProvider);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            expiry.Token);
        var frames = Channel.CreateBounded<WarmUpProgress>(LatestOnly);
        var warming = WarmAsync(scope, frames.Writer, lifetime.Token);
        try
        {
            await foreach (var frame in ReadAsync(frames.Reader, cancellationToken))
            {
                yield return frame;
            }
        }
        finally
        {
            // The reader may have left first: the waits stop before their tokens are disposed.
            await lifetime.CancelAsync();
            await warming;
        }
    }

    // The code a failed frame tells: an outage upstream, or a run cut short (stream expired,
    // client gone) whose work goes on for the next visit. No refusal gets here: the gateway
    // answers the catalog's own as pending, and the images stage ends done on its own.
    private static string? FailureCode(Exception exception) => exception switch
    {
        EgressException => CatalogProblem.UpstreamUnavailable().Code,
        OperationCanceledException => CatalogProblem.Pending().Code,
        _ => null,
    };

    // Never throws: a failure ends the frames with a failed one, an unexpected one completes
    // them with it, which hands it to the stream.
    private async Task WarmAsync(
        WarmUpScope scope,
        ChannelWriter<WarmUpProgress> frames,
        CancellationToken cancellationToken)
    {
        var tracker = new WarmUpTracker(scope.Resources, frames);
        try
        {
            tracker.Prepare();
            var read = await gateway.ReadAsync(scope.Catalog, cancellationToken);
            if (read.IsOpen)
            {
                await WarmImagesAsync(read.Context, tracker, cancellationToken);
            }
            else
            {
                tracker.Fail(read.Problem.Code);
            }

            frames.TryComplete();
        }
        catch (Exception exception) when (FailureCode(exception) is { } code)
        {
            tracker.Fail(code);
            frames.TryComplete();
        }
        catch (Exception exception)
        {
            frames.TryComplete(exception);
        }
    }

    // One resource after the other, so that their rows turn ready in the order shown. The
    // first refusal ends the run: the rows it did not reach stay unready.
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
            var watched = new WatchedImages
            {
                Version = context.Catalog.Version,
                Images = tracker.PendingOf(resource),
                Progress = tracker,
            };
            if (!await SettleAsync(watched, cancellationToken))
            {
                break;
            }

            tracker.Complete(resource);
        }

        tracker.Finish();
    }

    // False when the work was refused: what landed before stays, the rest is left pending.
    // The ingestion counted the refusal once; asking for more would only count it again.
    private async Task<bool> SettleAsync(
        WatchedImages watched,
        CancellationToken cancellationToken)
    {
        if (watched.Images.Count == 0)
        {
            return true;
        }

        try
        {
            await onDemand.EnsureImagesAsync(watched, cancellationToken);
            return true;
        }
        catch (OnDemandRefusedException)
        {
            return false;
        }
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
