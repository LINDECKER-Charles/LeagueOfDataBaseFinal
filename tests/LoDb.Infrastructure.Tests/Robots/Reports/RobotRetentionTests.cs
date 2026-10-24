using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Robots.Retention;
using LoDb.Infrastructure.Tests.Robots.Support;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using static LoDb.Infrastructure.Tests.Robots.Support.JournalSamples;

namespace LoDb.Infrastructure.Tests.Robots.Reports;

/// <summary>The hits past 30 days are deleted; their days keep their counts.</summary>
public sealed class RobotRetentionTests(PostgresContainerFixture postgres)
    : RobotsDatabase(postgres)
{
    [Fact]
    public async Task OldHitsGoTheirDaysStay()
    {
        var now = Time.GetUtcNow();
        await JournalAsync(
            Record(now.AddDays(-31), UserAgents.Bingbot, ("192.0.2.3", "/fr/items", 200)),
            Record(now.AddDays(-1), UserAgents.Bingbot, ("192.0.2.3", "/fr/runes", 200)));

        var deleted = await Get<IRobotRetention>().ApplyAsync(Cancellation);

        Assert.Equal(1, deleted);
        await using var context = await Get<IDbContextFactory<LoDbDbContext>>()
            .CreateDbContextAsync(Cancellation);
        Assert.Equal("/fr/runes", (await context.RobotHits.SingleAsync(Cancellation)).Target);
        Assert.Equal(2, await context.RobotDays.SumAsync(static day => day.Hits, Cancellation));
    }
}
