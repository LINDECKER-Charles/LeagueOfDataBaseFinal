using LoDb.Api.Modules.PublicApi.Access;
using LoDb.Api.Modules.PublicApi.Limits;

namespace LoDb.Api.Tests.PublicApi.Units;

/// <summary>
/// The quota of a key, then its credits: what one instance counts between two reads of the
/// key, and what a later read brings.
/// </summary>
public sealed class KeyLedgerTests
{
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateOnly October = new(2026, 10, 1);

    private readonly KeyLedger _ledger = new();

    [Fact]
    public void ThePlanPaysUntilTheQuotaIsReached()
    {
        var key = Key(quota: 3, used: 1);

        Assert.Equal(
            [QuotaDecision.Plan, QuotaDecision.Plan, QuotaDecision.Denied],
            Charge(key, 3));
    }

    [Fact]
    public void CreditsPayOnceTheQuotaIsSpent()
    {
        var key = Key(quota: 1, used: 1, credits: 2);

        Assert.Equal(QuotaDecision.NeedsCredit, _ledger.TryCharge(key, September));
        _ledger.RecordCredit(key, 1, September);
        Assert.Equal(QuotaDecision.NeedsCredit, _ledger.TryCharge(key, September));
        _ledger.RecordCredit(key, 0, September);

        Assert.Equal(QuotaDecision.Denied, _ledger.TryCharge(key, September));
    }

    [Fact]
    public void NoCreditLeftInTheDatabaseEndsTheCredits()
    {
        var key = Key(quota: 0, used: 0, credits: 5);
        Assert.Equal(QuotaDecision.NeedsCredit, _ledger.TryCharge(key, September));

        _ledger.RecordCredit(key, null, September);

        Assert.Equal(QuotaDecision.Denied, _ledger.TryCharge(key, September));
    }

    [Fact]
    public void ALaterReadBringsATopUp()
    {
        var spent = Key(quota: 0, used: 0);
        Assert.Equal(QuotaDecision.Denied, _ledger.TryCharge(spent, September));

        var toppedUp = spent with { CreditsBalance = 10, Sequence = 2 };

        Assert.Equal(QuotaDecision.NeedsCredit, _ledger.TryCharge(toppedUp, September));
    }

    [Fact]
    public void AnEarlierReadChangesNothing()
    {
        var fresh = Key(quota: 0, used: 0, sequence: 2);
        Assert.Equal(QuotaDecision.Denied, _ledger.TryCharge(fresh, September));

        var stale = fresh with { CreditsBalance = 10, Sequence = 1 };

        Assert.Equal(QuotaDecision.Denied, _ledger.TryCharge(stale, September));
    }

    [Fact]
    public void ACreditTakenWithAnEarlierReadLeavesTheLaterBalance()
    {
        var before = Key(quota: 0, used: 0, credits: 1);
        Assert.Equal(QuotaDecision.NeedsCredit, _ledger.TryCharge(before, September));
        var toppedUp = before with { CreditsBalance = 10, Sequence = 2 };
        Assert.Equal(QuotaDecision.NeedsCredit, _ledger.TryCharge(toppedUp, September));

        // The request admitted with the first read took the last credit it knew of.
        _ledger.RecordCredit(before, 0, September);

        Assert.Equal(QuotaDecision.NeedsCredit, _ledger.TryCharge(toppedUp, September));
    }

    [Fact]
    public void RequestsNotWrittenYetStillCount()
    {
        var key = Key(quota: 3, used: 0);
        Charge(key, 2);

        var reloaded = key with { Sequence = 2 };

        Assert.Equal([QuotaDecision.Plan, QuotaDecision.Denied], Charge(reloaded, 2));
    }

    [Fact]
    public void RequestsCountedElsewhereCountWhenMore()
    {
        var key = Key(quota: 3, used: 0);
        Charge(key, 1);

        var reloaded = key with { UsedThisMonth = 3, Sequence = 2 };

        Assert.Equal(QuotaDecision.Denied, _ledger.TryCharge(reloaded, September));
    }

    [Fact]
    public void ANewMonthStartsAtZero()
    {
        var key = Key(quota: 1, used: 1);
        Assert.Equal(QuotaDecision.Denied, _ledger.TryCharge(key, September));

        Assert.Equal(QuotaDecision.Plan, _ledger.TryCharge(key, October));
    }

    [Fact]
    public void AReadOfTheLastMonthDoesNotCountInTheNewOne()
    {
        var read = Key(quota: 1, used: 1);

        Assert.Equal(QuotaDecision.Plan, _ledger.TryCharge(read, October));
    }

    [Fact]
    public void ConcurrentRequestsNeverOvershootTheQuota()
    {
        var key = Key(quota: 500, used: 0);
        var plan = 0;

        Parallel.For(0, 1000, _ =>
        {
            if (_ledger.TryCharge(key, September) == QuotaDecision.Plan)
            {
                Interlocked.Increment(ref plan);
            }
        });

        Assert.Equal(500, plan);
    }

    private static ApiKeySnapshot Key(int quota, long used, long credits = 0, long sequence = 1) =>
        new(1, true, quota, credits, 60, used, September, sequence);

    private List<QuotaDecision> Charge(ApiKeySnapshot key, int requests) =>
        [.. Enumerable.Range(0, requests).Select(_ => _ledger.TryCharge(key, September))];
}
