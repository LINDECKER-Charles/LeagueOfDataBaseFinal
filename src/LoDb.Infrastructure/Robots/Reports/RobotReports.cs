using System.Globalization;
using LoDb.Infrastructure.Analytics.Reports;
using LoDb.Infrastructure.Robots.Reports.Detail;
using LoDb.Infrastructure.Robots.Reports.Folding;
using LoDb.Infrastructure.Robots.Reports.Journal;
using LoDb.Infrastructure.Robots.Reports.Reading;
using LoDb.Infrastructure.Robots.Reports.Views;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Robots.Reports;

/// <summary>
/// <see cref="IRobotReports"/>: the daily counts for the figures and the days, the hits kept
/// for the paths and the journal, the robot of each row read from its agent when it runs.
/// </summary>
internal sealed class RobotReports(
    RobotDailyReader daily,
    RobotHitReader hits,
    IOptions<RobotsOptions> options,
    TimeProvider timeProvider) : IRobotReports
{
    private const int PathLimit = 50;
    private const int RecentLimit = 20;
    private const int PageSize = 50;
    private const int MaxPage = 1_000;

    public async Task<RobotReport> BuildAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        var period = await PeriodOfAsync(range, cancellationToken);
        var map = await daily.AgentsAsync(cancellationToken);
        var cells = await daily.CellsAsync(period, selection: null, cancellationToken);
        var series = await daily.SeriesAsync(period, selection: null, cancellationToken);
        var robots = RobotSummaries.Of(cells, map);
        return new RobotReport
        {
            Range = range.Name,
            From = period.From,
            To = period.To,
            Days = period.Days,
            HitsSince = HitsSince(),
            Totals = RobotBreakdowns.TotalsOf(cells, robots),
            Dates = period.Dates,
            CategorySeries = RobotSeriesFold.ByCategory(series, map, period),
            Categories = RobotBreakdowns.CategoriesOf(robots),
            Robots = robots,
            Statuses = StatusesOf(cells),
            Sections = RobotBreakdowns.Rank(cells, static cell => cell.Section),
            Verdicts = RobotBreakdowns.Rank(cells, static cell => cell.Verdict),
            Networks = RobotBreakdowns.NetworksOf(cells, map),
        };
    }

    public async Task<RobotDetail?> DetailAsync(
        string key,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        var map = await daily.AgentsAsync(cancellationToken);
        if (map.SelectionOf(key) is not { } selection)
        {
            return null;
        }

        var period = await PeriodOfAsync(range, cancellationToken);
        var cells = await daily.CellsAsync(period, selection, cancellationToken);
        if (cells.Count == 0)
        {
            return null;
        }

        var series = await daily.SeriesAsync(period, selection, cancellationToken);
        var total = await daily.TotalAsync(period, cancellationToken);
        var lists = await ListsOfAsync(selection, period, map, cancellationToken);
        return DetailOf(new DetailSource(range, period, cells, series, map, total), lists);
    }

    public async Task<RobotHitPage> HitsAsync(
        RobotHitQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var map = await daily.AgentsAsync(cancellationToken);
        var page = Math.Clamp(query.Page, 1, MaxPage);
        var selection = SelectionOf(query, map);
        if (query.Robot is not null && selection is null)
        {
            return new RobotHitPage { Items = [], Page = page, HasMore = false };
        }

        var rows = await hits.PageAsync(
            selection,
            query,
            ((page - 1) * PageSize, PageSize + 1),
            cancellationToken);
        return new RobotHitPage
        {
            Items = [.. rows.Take(PageSize).Select(row => HitViews.Of(row, map))],
            Page = page,
            HasMore = rows.Count > PageSize,
        };
    }

    private static RobotSelection? SelectionOf(RobotHitQuery query, AgentMap map) =>
        query.Robot is { } robot ? map.SelectionOf(robot)
        : query.Category is { } category ? map.SelectionOfCategory(category)
        : null;

    private async Task<DetailLists> ListsOfAsync(
        RobotSelection selection,
        DatePeriod period,
        AgentMap map,
        CancellationToken cancellationToken)
    {
        var since = period.From > HitsSince() ? period.From : HitsSince();
        var paths = await hits.PathsAsync(
            selection,
            new DateTimeOffset(since.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            PathLimit,
            cancellationToken);
        var recent = await hits.PageAsync(
            selection,
            new RobotHitQuery(),
            (0, RecentLimit),
            cancellationToken);
        return new DetailLists(paths, [.. recent.Select(row => HitViews.Of(row, map))]);
    }

    private RobotDetail DetailOf(DetailSource read, DetailLists lists)
    {
        var first = read.Cells[0];
        var profile = read.Map.ProfileOf(first.AgentId, first.Detection, first.Network);
        return new RobotDetail
        {
            Range = read.Range.Name,
            From = read.Period.From,
            To = read.Period.To,
            HitsSince = HitsSince(),
            Robot = RobotSummaries.SummaryOf(profile, read.Cells, read.Total),
            VerifyDomains = profile.VerifyDomains,
            Dates = read.Period.Dates,
            StatusSeries = RobotSeriesFold.ByStatusClass(read.Series, read.Period),
            Statuses = StatusesOf(read.Cells),
            Sections = RobotBreakdowns.Rank(read.Cells, static cell => cell.Section),
            Verdicts = RobotBreakdowns.Rank(read.Cells, static cell => cell.Verdict),
            Networks = RobotBreakdowns.NetworksOf(read.Cells, read.Map),
            Agents = DetailAgents.Of(read.Cells, read.Map),
            Paths = lists.Paths,
            Recent = lists.Recent,
        };
    }

    private static IReadOnlyList<AnalyticsRank> StatusesOf(IReadOnlyList<DailyCell> cells) =>
        RobotBreakdowns.Rank(
            cells,
            static cell => cell.Status.ToString(CultureInfo.InvariantCulture));

    private async Task<DatePeriod> PeriodOfAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (range.Days is { } span)
        {
            return new DatePeriod(today.AddDays(1 - span), today);
        }

        var earliest = await daily.EarliestAsync(cancellationToken);
        return new DatePeriod(earliest is { } day && day < today ? day : today, today);
    }

    private DateOnly HitsSince() => DateOnly.FromDateTime(
        (timeProvider.GetUtcNow() - options.Value.HitRetention).UtcDateTime);

    // What a detail is folded from: the robot's cells and days, the agents, every robot's hits.
    private sealed record DetailSource(
        AnalyticsRange Range,
        DatePeriod Period,
        IReadOnlyList<DailyCell> Cells,
        IReadOnlyList<SeriesCell> Series,
        AgentMap Map,
        long Total);

    // What a detail lists from the hits kept.
    private sealed record DetailLists(
        IReadOnlyList<RobotPathView> Paths,
        IReadOnlyList<RobotHitView> Recent);
}
