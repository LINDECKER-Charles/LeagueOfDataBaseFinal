using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Modules.Admin.Prerender.Http.History;

/// <summary>
/// The pace of a pass and when it should end: pure functions of its counters, its running
/// time and the time of the report.
/// </summary>
/// <remarks>
/// A unit's pages are listed only when the pass reaches it, so the pages still to come are
/// those of the listed units not rendered yet, plus each pending unit at the mean of the listed
/// ones: the units of a pass are the 21 locales of its versions, whose page counts differ
/// little.
/// </remarks>
internal static class PassProgress
{
    private const decimal SecondsPerMinute = 60m;
    private const int RateDecimals = 1;

    /// <summary>Seconds spent running up to <paramref name="now"/>, the pauses left out.</summary>
    public static long ActiveSeconds(PrerenderPass pass, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pass);
        var open = pass.RunningSince is { } since
            ? Math.Max(0, (long)(now - since).TotalSeconds)
            : 0;
        return pass.ActiveSeconds + open;
    }

    /// <summary>
    /// Pages written, skipped or failed per minute of running time; null before a whole minute
    /// of it, too short to tell a pace from the first burst.
    /// </summary>
    public static decimal? PagesPerMinute(PrerenderPass pass, long activeSeconds)
    {
        ArgumentNullException.ThrowIfNull(pass);
        return activeSeconds < SecondsPerMinute
            ? null
            : Processed(pass) * SecondsPerMinute / activeSeconds;
    }

    /// <summary>The rate as the panel shows it, to one decimal.</summary>
    public static decimal? Shown(decimal? pagesPerMinute) =>
        pagesPerMinute is { } rate ? Math.Round(rate, RateDecimals) : null;

    /// <summary>
    /// When the pass should end at <paramref name="pagesPerMinute"/>; null unless it runs at a
    /// known pace with some units listed.
    /// </summary>
    public static DateTimeOffset? EstimatedEnd(
        PassFacts facts,
        decimal? pagesPerMinute,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var pass = facts.Pass;
        var pending = facts.UnitsIn(PrerenderUnitState.Pending);
        var listed = facts.Units.Values.Sum() - pending;
        if (pass.State != PrerenderPassState.Running || pagesPerMinute is not > 0 || listed == 0)
        {
            return null;
        }

        var meanPerUnit = (decimal)pass.PagesTotal / listed;
        var remaining = Math.Max(0, pass.PagesTotal - Processed(pass)) + (pending * meanPerUnit);
        return now.AddMinutes((double)(remaining / pagesPerMinute.Value));
    }

    private static long Processed(PrerenderPass pass) =>
        pass.PagesWritten + pass.PagesSkipped + pass.PagesFailed;
}
