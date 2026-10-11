using System.Globalization;

namespace LoDb.Api.Hosting.SecurityReports.Summary;

/// <summary>
/// The <c>web.csp.violated</c> line: what browsers reported over one interval, per directive
/// and disposition, and the bodies refused or shed. Nothing of a page, a resource or a client.
/// </summary>
internal static partial class CspReportSummary
{
    private const string Separator = ", ";
    private const string None = "none";

    /// <summary>Writes the line of <paramref name="window"/>, unless nothing came in.</summary>
    public static void Write(ILogger logger, CspReportWindow window, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.IsEmpty)
        {
            return;
        }

        LogViolated(
            logger,
            window.Violations.Values.Sum(),
            (long)interval.TotalSeconds,
            DirectivesOf(window),
            RefusalsOf(window),
            window.Shed);
    }

    // The most reported first: "script-src-elem/enforce=3, trusted-types/report=1".
    private static string DirectivesOf(CspReportWindow window) =>
        Joined(window.Violations
            .OrderByDescending(static entry => entry.Value)
            .ThenBy(static entry => entry.Key.Directive, StringComparer.Ordinal)
            .Select(static entry =>
                $"{entry.Key.Directive}/{entry.Key.Disposition}={Count(entry.Value)}"));

    private static string RefusalsOf(CspReportWindow window) =>
        Joined(window.Refusals
            .OrderBy(static entry => entry.Key)
            .Select(static entry => $"{entry.Key}={Count(entry.Value)}"));

    private static string Joined(IEnumerable<string> parts)
    {
        var text = string.Join(Separator, parts);
        return text.Length == 0 ? None : text;
    }

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    [LoggerMessage(
        EventName = "web.csp.violated",
        Level = LogLevel.Warning,
        Message = "{Violations} policy violations reported in {IntervalSeconds} s: {Directives};"
            + " bodies refused: {Refusals}; shed: {Shed}.")]
    private static partial void LogViolated(
        ILogger logger,
        int violations,
        long intervalSeconds,
        string directives,
        string refusals,
        int shed);
}
