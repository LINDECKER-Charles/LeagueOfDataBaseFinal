using LoDb.Ingestion.Pipeline;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Ingestion.Tests.Queue;

/// <summary>
/// Crawlers queue a bounded number of requests per minute (C5); zero lets them queue none.
/// </summary>
public sealed class CrawlerBudgetTests
{
    private readonly FakeTimeProvider clock =
        new(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void BudgetRefillsEveryMinute()
    {
        var budget = Budget(perMinute: 2);

        Assert.True(budget.TryTake());
        Assert.True(budget.TryTake());
        Assert.False(budget.TryTake());
        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.False(budget.TryTake());
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(budget.TryTake());
    }

    [Fact]
    public void ZeroBudgetRefusesEveryCrawler()
    {
        var budget = Budget(perMinute: 0);

        Assert.False(budget.TryTake());
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(budget.TryTake());
    }

    private CrawlerBudget Budget(int perMinute) =>
        new(Options.Create(new IngestionOptions { CrawlerRequestsPerMinute = perMinute }), clock);
}
