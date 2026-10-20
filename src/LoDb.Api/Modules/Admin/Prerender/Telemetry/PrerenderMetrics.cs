using System.Diagnostics.Metrics;

namespace LoDb.Api.Modules.Admin.Prerender.Telemetry;

/// <summary>
/// Meter <c>LoDb.Prerender</c>: <c>lodb.prerender.pages</c>, exported as
/// <c>lodb_prerender_pages_total{outcome}</c>, one increment per page of the pass and one per
/// refusal of the SSR.
/// </summary>
/// <remarks>
/// A metric rather than a log line per page: a full pass renders four million of them. The
/// summary line of each unit gives the detail.
/// </remarks>
internal sealed class PrerenderMetrics
{
    public const string MeterName = "LoDb.Prerender";
    public const string OutcomeTag = "outcome";

    private readonly Counter<long> _pages;

    public PrerenderMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _pages = meterFactory.Create(MeterName).CreateCounter<long>(
            "lodb.prerender.pages",
            unit: "{page}",
            description: "Pages of the pre-rendering pass, by outcome.");
    }

    public void Count(PageOutcome outcome) =>
        _pages.Add(1, new KeyValuePair<string, object?>(OutcomeTag, TagOf(outcome)));

    private static string TagOf(PageOutcome outcome) => outcome switch
    {
        PageOutcome.Written => "written",
        PageOutcome.Skipped => "skipped",
        PageOutcome.Refused => "refused",
        PageOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
