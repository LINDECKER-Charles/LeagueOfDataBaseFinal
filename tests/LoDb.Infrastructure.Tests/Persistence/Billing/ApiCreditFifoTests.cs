using LoDb.Infrastructure.Persistence.PublicApi;

namespace LoDb.Infrastructure.Tests.Persistence.Billing;

/// <summary>
/// The balance belongs to the newest live grants: an expiring grant loses what the newer ones
/// leave of the balance, and a balance no grant covers is reported.
/// </summary>
public sealed class ApiCreditFifoTests
{
    private static readonly DateTimeOffset Start = new(2025, 1, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly ApiCreditGrant Oldest = Grant(1, months: 0, requests: 5_000);
    private static readonly ApiCreditGrant Middle = Grant(2, months: 3, requests: 10_000);
    private static readonly ApiCreditGrant Newest = Grant(3, months: 6, requests: 20_000);
    private static readonly ApiCreditGrant[] Live = [Oldest, Middle, Newest];

    [Theory]
    // Untouched balance: each grant keeps all of it.
    [InlineData(35_000, 5_000, 10_000, 20_000)]
    // Spent from the oldest first.
    [InlineData(32_000, 2_000, 10_000, 20_000)]
    [InlineData(25_000, 0, 5_000, 20_000)]
    [InlineData(12_000, 0, 0, 12_000)]
    [InlineData(0, 0, 0, 0)]
    // More than the grants: the surplus is uncovered, not theirs.
    [InlineData(40_000, 5_000, 10_000, 20_000)]
    public void BalanceBelongsToTheNewestGrants(
        long balance,
        long oldest,
        long middle,
        long newest)
    {
        Assert.Equal(oldest, ApiCreditFifo.RemainingOf(Oldest, balance, Live));
        Assert.Equal(middle, ApiCreditFifo.RemainingOf(Middle, balance, Live));
        Assert.Equal(newest, ApiCreditFifo.RemainingOf(Newest, balance, Live));
    }

    [Theory]
    [InlineData(40_000, 5_000)]
    [InlineData(35_000, 0)]
    [InlineData(1_000, 0)]
    public void UncoveredIsTheBalanceAboveTheLiveGrants(long balance, long uncovered) =>
        Assert.Equal(uncovered, ApiCreditFifo.Uncovered(balance, Live));

    [Fact]
    public void SettledGrantsNoLongerCount()
    {
        var settled = Grant(4, months: 9, requests: 50_000);
        settled.ExpiredAt = Start.AddMonths(21);
        settled.ExpiredRequests = 0;
        ApiCreditGrant[] grants = [.. Live, settled];

        Assert.Equal(2_000, ApiCreditFifo.RemainingOf(Oldest, 32_000, grants));
        Assert.Equal(8_000, ApiCreditFifo.Uncovered(43_000, grants));
    }

    [Fact]
    public void SameInstantIsOrderedById()
    {
        var first = Grant(10, months: 1, requests: 1_000);
        var second = Grant(11, months: 1, requests: 1_000);

        Assert.Equal(500, ApiCreditFifo.RemainingOf(first, 1_500, [second, first]));
        Assert.Equal(1_000, ApiCreditFifo.RemainingOf(second, 1_500, [second, first]));
    }

    [Fact]
    public void SettlingDueGrantsInAnyOrderTakesTheSameTotal()
    {
        const long Balance = 22_000;
        var oldestFirst = Settle(Balance, [Oldest, Middle]);
        var middleFirst = Settle(Balance, [Middle, Oldest]);

        Assert.Equal(2_000, oldestFirst);
        Assert.Equal(oldestFirst, middleFirst);
    }

    // Settles the grants in the given order, as the expiry job would, and returns what the
    // balance loses.
    private static long Settle(long balance, ApiCreditGrant[] due)
    {
        var live = Live.ToList();
        var removed = 0L;
        foreach (var grant in due)
        {
            var remaining = ApiCreditFifo.RemainingOf(grant, balance - removed, live);
            removed += remaining;
            live.Remove(grant);
        }

        return removed;
    }

    private static ApiCreditGrant Grant(long id, int months, long requests) => new()
    {
        Id = id,
        Source = ApiCreditGrantSource.Purchase,
        Requests = requests,
        PurchasedAt = Start.AddMonths(months),
        ExpiresAt = Start.AddMonths(months + ApiCreditGrant.ValidityMonths),
        StripeSessionId = $"cs_test_{id}",
    };
}
