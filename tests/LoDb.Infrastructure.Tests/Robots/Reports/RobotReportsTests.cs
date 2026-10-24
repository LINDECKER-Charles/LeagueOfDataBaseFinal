using LoDb.Infrastructure.Analytics.Reports;
using LoDb.Infrastructure.Robots.Identification;
using LoDb.Infrastructure.Robots.Reports;
using LoDb.Infrastructure.Robots.Reports.Journal;
using LoDb.Infrastructure.Robots.Verification;
using LoDb.Infrastructure.Tests.Robots.Support;
using LoDb.Testing;
using static LoDb.Infrastructure.Tests.Robots.Support.JournalSamples;

namespace LoDb.Infrastructure.Tests.Robots.Reports;

/// <summary>
/// The reports name every robot from its agent, fold its days, its statuses and its
/// networks, and the journal pages through the hits by robot, category and status.
/// </summary>
public sealed class RobotReportsTests(PostgresContainerFixture postgres)
    : RobotsDatabase(postgres)
{
    private static readonly AnalyticsRange Week = Range(AnalyticsRange.LastWeek);

    private IRobotReports Reports => Get<IRobotReports>();

    [Fact]
    public async Task TheReportNamesEveryRobotTheMostActiveFirst()
    {
        await WriteSampleAsync();

        var report = await Reports.BuildAsync(Week, Cancellation);

        Assert.Equal(7, report.Days);
        Assert.Equal(6, report.Totals.Figures.Hits);
        Assert.Equal(
            ["googlebot", "gptbot", "undeclared:example.net"],
            report.Robots.Select(static robot => robot.Key));
        var googlebot = report.Robots[0].Figures;
        Assert.Equal(
            (2L, 1L, 1L, 1L),
            (googlebot.Verified, googlebot.Mismatch, googlebot.ServerErrors, googlebot.Refused));
        Assert.Equal(6, report.Totals.CrawlerHits);
        var search = report.CategorySeries
            .Single(static line => line.Name == RobotCategories.Search);
        Assert.Equal(3, search.Values[^1]);
        Assert.Contains(report.Networks, static network => network.Domain == "googlebot.com");
    }

    [Fact]
    public async Task TheDetailOfARobotListsItsPathsAndAgents()
    {
        await WriteSampleAsync();

        var detail = await Reports.DetailAsync("googlebot", Week, Cancellation);

        Assert.NotNull(detail);
        // Its share is of every robot's hits: 3 of the sample's 6.
        Assert.Equal(50, detail.Robot.Pct);
        Assert.Equal(VerificationDomains.Google, detail.VerifyDomains);
        Assert.Equal("/fr/items", detail.Paths[0].Target);
        Assert.Equal(2, detail.Paths[0].Hits);
        Assert.Equal(UserAgents.Googlebot, Assert.Single(detail.Agents).Agent);
        Assert.Equal(3, detail.Recent.Count);
        Assert.Equal([2L, 0L, 0L, 1L], detail.StatusSeries.Select(static line => line.Values[^1]));
    }

    [Fact]
    public async Task AnUndeclaredRobotIsReadByItsNetworkAndAnUnknownKeyByNothing()
    {
        await WriteSampleAsync();

        var undeclared = await Reports.DetailAsync("undeclared:example.net", Week, Cancellation);
        var unknown = await Reports.DetailAsync("nobot", Week, Cancellation);

        Assert.Equal(RobotCategories.Undeclared, undeclared?.Robot.Category);
        Assert.Equal("example.net", undeclared?.Robot.Operator);
        Assert.Null(unknown);
    }

    [Theory]
    [InlineData("gptbot", null, null, 2)]
    [InlineData(null, RobotCategories.Search, null, 3)]
    [InlineData(null, null, 5, 1)]
    [InlineData("undeclared:example.net", null, null, 1)]
    [InlineData("nobot", null, null, 0)]
    public async Task TheJournalFiltersItsHits(
        string? robot,
        string? category,
        int? statusClass,
        int count)
    {
        await WriteSampleAsync();

        var page = await Reports.HitsAsync(
            new RobotHitQuery { Robot = robot, Category = category, StatusClass = statusClass },
            Cancellation);

        Assert.Equal(count, page.Items.Count);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task TheJournalSearchesThePathAndNamesTheRobot()
    {
        await WriteSampleAsync();

        var page = await Reports.HitsAsync(
            new RobotHitQuery { TargetPattern = "%runes%", Verdict = RobotVerdicts.Unverifiable },
            Cancellation);

        var hit = Assert.Single(page.Items);
        Assert.Equal(("gptbot", "GPTBot", "OpenAI"), (hit.RobotKey, hit.RobotName, hit.Operator));
    }

    // Googlebot from its own network twice and from a cloud once, GPTBot twice, a browser at a
    // robot's pace once past the threshold of 3.
    private async Task WriteSampleAsync()
    {
        var at = Time.GetUtcNow();
        await JournalAsync(
            Record(at, UserAgents.Googlebot, (GoogleAddress, "/fr/items", 200)),
            Record(at.AddSeconds(1), UserAgents.Googlebot, (GoogleAddress, "/fr/items", 200)),
            Record(at.AddSeconds(2), UserAgents.Googlebot, (CloudAddress, "/fr/champions", 503)),
            Record(at, UserAgents.GptBot, ("192.0.2.9", "/fr/runes", 200)),
            Record(at, UserAgents.GptBot, ("192.0.2.9", "/robots.txt", 200)),
            Record(at, UserAgents.Firefox, (CloudAddress, "/fr/10.1.1/items", 200)),
            Record(at, UserAgents.Firefox, (CloudAddress, "/fr/11.1.1/items", 200)),
            Record(at, UserAgents.Firefox, (CloudAddress, "/fr/12.1.1/items", 200)));
    }

    private static AnalyticsRange Range(string name) =>
        AnalyticsRange.TryParse(name, out var range) ? range : throw new ArgumentException(name);
}
