using LoDb.Api.Modules.Admin.Prerender.Options;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Sharing;

/// <summary>
/// The renders in flight on the pass's SSR, shared by the pages of a manual pass and those of
/// the automatic queue: <see cref="PrerenderOptions.MaxInFlight"/> at most, the bound of the
/// SSR's lane, beyond which it answers a 503 and its Retry-After of half a minute.
/// </summary>
/// <remarks>
/// The slots are handed out in the order they were asked for, so that neither side starves.
/// While a pass renders (a page of its claimed one in the last <see cref="PassMemory"/>), the
/// queue holds <see cref="DemandOptions.QueueSharePercent"/> of the slots at most, one at
/// least; the pass takes the others, and every slot the queue leaves. With no pass, the queue
/// takes them all. The pace stays one for both: each render then waits its turn at
/// <c>RenderPacer</c>.
/// </remarks>
internal sealed class RenderSlots(
    IOptions<PrerenderOptions> pass,
    IOptions<DemandOptions> demand,
    TimeProvider clock)
{
    /// <summary>How long a pass counts as rendering after its last page claimed a slot.</summary>
    public static readonly TimeSpan PassMemory = TimeSpan.FromMinutes(1);

    private const int AllPercent = 100;

    private readonly Lock _gate = new();
    private readonly LinkedList<Waiter> _waiters = new();
    private readonly Dictionary<RenderClaimant, int> _held = new()
    {
        [RenderClaimant.Pass] = 0,
        [RenderClaimant.Queue] = 0,
    };

    private long? _passClaimedAt;

    /// <summary>Slots the queue may hold now.</summary>
    public int QueueLimit
    {
        get
        {
            lock (_gate)
            {
                return QueueLimitNow();
            }
        }
    }

    /// <summary>
    /// Waits for a slot of <paramref name="claimant"/>'s; disposing the handle frees it.
    /// </summary>
    public async Task<IDisposable> EnterAsync(
        RenderClaimant claimant,
        CancellationToken cancellationToken)
    {
        var waiter = new Waiter(claimant);
        lock (_gate)
        {
            if (claimant == RenderClaimant.Pass)
            {
                _passClaimedAt = clock.GetTimestamp();
            }

            waiter.Node = _waiters.AddLast(waiter);
            GrantWaiting();
        }

        await using (cancellationToken.Register(() => Cancel(waiter, cancellationToken)))
        {
            await waiter.Granted.Task.ConfigureAwait(false);
        }

        return new Slot(this, claimant);
    }

    private int Total => pass.Value.MaxInFlight;

    // Under the gate: the waiters that may enter now, in the order they came.
    private void GrantWaiting()
    {
        for (var node = _waiters.First; node is not null;)
        {
            var next = node.Next;
            if (CanEnter(node.Value.Claimant))
            {
                _waiters.Remove(node);
                _held[node.Value.Claimant]++;
                node.Value.Granted.TrySetResult();
            }

            node = next;
        }
    }

    private bool CanEnter(RenderClaimant claimant) =>
        _held[RenderClaimant.Pass] + _held[RenderClaimant.Queue] < Total
        && (claimant == RenderClaimant.Pass || _held[RenderClaimant.Queue] < QueueLimitNow());

    private int QueueLimitNow()
    {
        var isPassRendering = _held[RenderClaimant.Pass] > 0
            || (_passClaimedAt is { } at && clock.GetElapsedTime(at) < PassMemory);
        return isPassRendering
            ? Math.Max(1, Total * demand.Value.QueueSharePercent / AllPercent)
            : Total;
    }

    private void Cancel(Waiter waiter, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (waiter.Node?.List is null)
            {
                return;
            }

            _waiters.Remove(waiter.Node);
        }

        waiter.Granted.TrySetCanceled(cancellationToken);
    }

    private void Release(RenderClaimant claimant)
    {
        lock (_gate)
        {
            _held[claimant]--;
            GrantWaiting();
        }
    }

    private sealed class Waiter(RenderClaimant claimant)
    {
        public RenderClaimant Claimant { get; } = claimant;

        public TaskCompletionSource Granted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LinkedListNode<Waiter>? Node { get; set; }
    }

    private sealed class Slot(RenderSlots slots, RenderClaimant claimant) : IDisposable
    {
        private int _isFreed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isFreed, 1) == 0)
            {
                slots.Release(claimant);
            }
        }
    }
}
