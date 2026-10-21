using System.Diagnostics.Metrics;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;

/// <summary>
/// The regulator's metrics, on meter <c>LoDb.Prerender</c>:
/// <c>lodb_prerender_pace_renders_per_minute</c> (the allowance in force),
/// <c>lodb_prerender_pace_limit{reason}</c> (1 for the reason of the moment, 0 for the others)
/// and <c>lodb_prerender_pace_changes_total{change, reason}</c>.
/// </summary>
/// <remarks>
/// Both gauges exist only while a pass drains on this instance: another instance, or this one
/// between two passes, reports none, so that a sum over the stack is the pace of its pass.
/// </remarks>
internal sealed class PaceMetrics
{
    private const string ChangeTag = "change";
    private const string ReasonTag = "reason";

    private readonly Counter<long> _changes;
    private Func<PaceSnapshot?> _read = static () => null;

    public PaceMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(PrerenderMetrics.MeterName);
        _changes = meter.CreateCounter<long>(
            "lodb.prerender.pace.changes",
            unit: "{change}",
            description: "Moves of the pre-rendering pace, by change and reason.");
        meter.CreateObservableGauge(
            "lodb.prerender.pace.renders_per_minute",
            RendersPerMinute,
            unit: "{render}",
            description: "Renders a minute the pre-rendering pass may start now.");
        meter.CreateObservableGauge(
            "lodb.prerender.pace.limit",
            Limits,
            description: "What limits the pre-rendering pace now: 1 for its reason.");
    }

    /// <summary>Reads the regulator's state at each collect.</summary>
    public void Follow(Func<PaceSnapshot?> read) => _read = read;

    public void Count(PaceChange change, PaceReason reason) => _changes.Add(
        1,
        new KeyValuePair<string, object?>(ChangeTag, PaceTags.TagOf(change)),
        new KeyValuePair<string, object?>(ReasonTag, PaceTags.TagOf(reason)));

    private IEnumerable<Measurement<int>> RendersPerMinute() =>
        _read() is { } snapshot ? [new Measurement<int>(snapshot.RendersPerMinute)] : [];

    private IEnumerable<Measurement<int>> Limits() =>
        _read() is { } snapshot
            ? PaceTags.Reasons.Select(reason => new Measurement<int>(
                reason == snapshot.Reason ? 1 : 0,
                new KeyValuePair<string, object?>(ReasonTag, PaceTags.TagOf(reason))))
            : [];
}
