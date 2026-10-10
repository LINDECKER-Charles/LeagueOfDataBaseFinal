using LoDb.Ingestion.Pipeline;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Ingestion.Tests.Queue;

/// <summary>
/// The budgets of the on-demand work: a bounded number of units per minute, refilled with
/// the next window; zero lets none through; a wish larger than what is left gets the rest.
/// </summary>
public sealed class MinuteBudgetTests
{
    private readonly FakeTimeProvider clock =
        new(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void BudgetRefillsEveryMinute()
    {
        var budget = new MinuteBudget(2, clock);

        Assert.True(budget.TryTake());
        Assert.True(budget.TryTake());
        Assert.False(budget.TryTake());
        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.False(budget.TryTake());
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(budget.TryTake());
    }

    [Fact]
    public void ZeroBudgetRefusesEverything()
    {
        var budget = new MinuteBudget(0, clock);

        Assert.False(budget.TryTake());
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0, budget.TakeUpTo(10));
    }

    [Fact]
    public void LargerWishGetsWhatIsLeft()
    {
        var budget = new MinuteBudget(10, clock);

        Assert.Equal(6, budget.TakeUpTo(6));
        Assert.Equal(4, budget.TakeUpTo(6));
        Assert.Equal(0, budget.TakeUpTo(1));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(10, budget.TakeUpTo(25));
    }

    [Fact]
    public void BudgetsFollowTheirSettings()
    {
        var budgets = new OnDemandBudgets(
            Options.Create(new IngestionOptions
            {
                CrawlerRequestsPerMinute = 1,
                ColdDatasetsPerMinute = 2,
                ColdImagesPerMinute = 3,
            }),
            clock);

        Assert.Equal(1, budgets.Crawler.TakeUpTo(10));
        Assert.Equal(2, budgets.Datasets.TakeUpTo(10));
        Assert.Equal(3, budgets.Images.TakeUpTo(10));
    }
}
