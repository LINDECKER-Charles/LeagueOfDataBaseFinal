using LoDb.Api.Modules.Admin.Prerender.Demand;
using LoDb.Api.Modules.Admin.Prerender.Demand.Sharing;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Tests.Prerender.Support;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// The pass SSR's slots: the queue takes them all while no pass renders, its share while one
/// does, one at least; neither side starves, and a waiter cancelled holds nothing.
/// </summary>
public sealed class RenderSlotsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueueAloneTakesEverySlot()
    {
        var slots = Slots(total: 4, share: 50);

        var held = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => slots.EnterAsync(RenderClaimant.Queue, Token)));

        Assert.Equal(4, held.Length);
        Assert.Equal(4, slots.QueueLimit);
    }

    [Fact]
    public async Task WhileAPassRendersTheQueueKeepsToItsShare()
    {
        var slots = Slots(total: 4, share: 50);
        using var pass = await slots.EnterAsync(RenderClaimant.Pass, Token);
        using var first = await slots.EnterAsync(RenderClaimant.Queue, Token);
        using var second = await slots.EnterAsync(RenderClaimant.Queue, Token);

        var third = slots.EnterAsync(RenderClaimant.Queue, Token);
        using var passAgain = await slots.EnterAsync(RenderClaimant.Pass, Token);

        Assert.Equal(2, slots.QueueLimit);
        Assert.False(third.IsCompleted);
        first.Dispose();
        (await third).Dispose();
    }

    [Fact]
    public async Task QueueGetsOneSlotAtLeastBesideAPass()
    {
        var slots = Slots(total: 1, share: 10);
        var pass = await slots.EnterAsync(RenderClaimant.Pass, Token);

        var queue = slots.EnterAsync(RenderClaimant.Queue, Token);
        var passNext = slots.EnterAsync(RenderClaimant.Pass, Token);
        pass.Dispose();
        var queued = await queue;

        Assert.False(passNext.IsCompleted);
        queued.Dispose();
        (await passNext).Dispose();
    }

    [Fact]
    public async Task PassStillRendersAfterItsLastSlotForAMinute()
    {
        var clock = new InstantClock();
        var slots = Slots(total: 2, share: 50, clock);
        (await slots.EnterAsync(RenderClaimant.Pass, Token)).Dispose();

        Assert.Equal(1, slots.QueueLimit);
        clock.Advance(RenderSlots.PassMemory);
        Assert.Equal(2, slots.QueueLimit);
    }

    [Fact]
    public async Task CancelledWaiterHoldsNothing()
    {
        var slots = Slots(total: 1, share: 100);
        var held = await slots.EnterAsync(RenderClaimant.Queue, Token);
        using var cancel = new CancellationTokenSource();
        var waiting = slots.EnterAsync(RenderClaimant.Queue, cancel.Token);

        await cancel.CancelAsync();
        held.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        using var next = await slots.EnterAsync(RenderClaimant.Queue, Token);
    }

    private static RenderSlots Slots(int total, int share, TimeProvider? clock = null) => new(
        Options.Create(new PrerenderOptions { MaxInFlight = total }),
        Options.Create(new DemandOptions { QueueSharePercent = share }),
        clock ?? TimeProvider.System);
}
