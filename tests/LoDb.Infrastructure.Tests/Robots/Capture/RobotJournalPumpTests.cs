using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Robots.Detection;
using LoDb.Infrastructure.Robots.Verification;
using LoDb.Infrastructure.Tests.Robots.Support;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using static LoDb.Infrastructure.Tests.Robots.Support.JournalSamples;

namespace LoDb.Infrastructure.Tests.Robots.Capture;

/// <summary>
/// From nginx's records to the journal: a robot's request is a hit, verified by the network
/// of its address; a reader's is dropped; a browser at a robot's pace becomes one; the days
/// sum what their hits say. Nothing of an address is kept.
/// </summary>
public sealed class RobotJournalPumpTests(PostgresContainerFixture postgres)
    : RobotsDatabase(postgres)
{
    [Fact]
    public async Task ARobotIsAHitAndAReaderIsNot()
    {
        var at = Time.GetUtcNow();

        var written = await JournalAsync(
            Record(at, UserAgents.Googlebot, (GoogleAddress, "/fr/16.1.1/items", 200)),
            Record(at, UserAgents.Chrome, ("192.0.2.1", "/fr/items", 200)));

        Assert.Equal(1, written);
        await using var context = await ContextAsync();
        var hit = await context.RobotHits.SingleAsync(Cancellation);
        Assert.Equal(
            (RobotDetections.Agent, RobotVerdicts.Verified, "googlebot.com", HitSections.Archive),
            (hit.Detection, hit.Verdict, hit.Network, hit.Section));
        var agent = await context.RobotAgents.SingleAsync(Cancellation);
        Assert.Equal(UserAgents.Googlebot, agent.Agent);
    }

    [Fact]
    public async Task AGooglebotOfAnotherNetworkIsUnmasked()
    {
        await JournalAsync(Record(
            Time.GetUtcNow(),
            UserAgents.Googlebot,
            (CloudAddress, "/fr/items", 200)));

        await using var context = await ContextAsync();
        var hit = await context.RobotHits.SingleAsync(Cancellation);
        Assert.Equal((RobotVerdicts.Mismatch, "example.net"), (hit.Verdict, hit.Network));
    }

    [Fact]
    public async Task ABrowserAtARobotsPaceBecomesOneAtTheThreshold()
    {
        var at = Time.GetUtcNow();
        var pages = Enumerable.Range(0, 4)
            .Select(page => Record(
                at.AddSeconds(page),
                UserAgents.Firefox,
                (CloudAddress, $"/fr/1{page}.1.1/champions", 200)))
            .ToArray();

        var written = await JournalAsync(pages);

        // The threshold of the tests is 3: the third request gives the client away.
        Assert.Equal(2, written);
        await using var context = await ContextAsync();
        Assert.All(
            await context.RobotHits.ToListAsync(Cancellation),
            static hit => Assert.Equal(
                (RobotDetections.Rate, RobotVerdicts.Unverifiable, "example.net"),
                (hit.Detection, hit.Verdict, hit.Network)));
    }

    [Fact]
    public async Task TheDaysSumTheirHits()
    {
        var at = Time.GetUtcNow();
        await JournalAsync(
            Record(at, UserAgents.Googlebot, (GoogleAddress, "/fr/items", 200)),
            Record(at, UserAgents.Googlebot, (GoogleAddress, "/fr/runes", 200)));
        await JournalAsync(
            Record(at, UserAgents.Googlebot, (GoogleAddress, "/fr/summoners", 200)),
            Record(at, UserAgents.Googlebot, (GoogleAddress, "/fr/champions", 503)));

        await using var context = await ContextAsync();
        var days = await context.RobotDays
            .OrderBy(static day => day.Status)
            .ToListAsync(Cancellation);
        Assert.Equal([3L, 1L], days.Select(static day => day.Hits));
        Assert.Equal(360, days[0].DurationMs);
        Assert.Equal(4, await context.RobotHits.CountAsync(Cancellation));
    }

    private async Task<LoDbDbContext> ContextAsync() =>
        await Get<IDbContextFactory<LoDbDbContext>>().CreateDbContextAsync(Cancellation);
}
