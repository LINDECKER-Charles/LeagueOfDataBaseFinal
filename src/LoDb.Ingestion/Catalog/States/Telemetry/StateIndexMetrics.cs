using System.Diagnostics.Metrics;
using LoDb.Domain.States;
using LoDb.Ingestion.Catalog.States.Updates;

namespace LoDb.Ingestion.Catalog.States.Telemetry;

/// <summary>
/// Meter <c>LoDb.States</c>: what the writer of the index of states does, by trigger and
/// outcome, the states it opens and closes, and how the copy each process holds loads.
/// </summary>
/// <remarks>
/// No version, entity nor key in a tag: the values are the closed sets below. The Prometheus
/// names are <c>lodb_states_*</c>.
/// </remarks>
internal sealed class StateIndexMetrics
{
    public const string MeterName = "LoDb.States";
    public const string TriggerTag = "trigger";
    public const string OutcomeTag = "outcome";
    public const string KindTag = "kind";

    public const string BuildTrigger = "build";
    public const string PromotionTrigger = "promotion";
    public const string ReconcileTrigger = "reconcile";

    public const string UnchangedOutcome = "unchanged";
    public const string ExtendedOutcome = "extended";
    public const string BuiltOutcome = "built";
    public const string LockedOutcome = "locked";
    public const string FailedOutcome = "failed";

    public const string LoadedOutcome = "loaded";

    public const string NewKind = "new";
    public const string ChangedKind = "changed";
    public const string ReturnedKind = "returned";

    // From a no-op of a few milliseconds to a whole rebuild, minutes long.
    private static readonly double[] UpdateBuckets = [0.01, 0.05, 0.25, 1, 5, 30, 120, 600];

    private readonly Counter<long> updates;
    private readonly Histogram<double> updateDuration;
    private readonly Counter<long> versionsIndexed;
    private readonly Counter<long> opened;
    private readonly Counter<long> closed;
    private readonly Counter<long> copyLoads;
    private readonly Counter<long> unavailable;

    public StateIndexMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        updates = meter.CreateCounter<long>(
            "lodb.states.updates", "{update}", "Updates of the index of states, by outcome.");
        updateDuration = meter.CreateHistogram(
            "lodb.states.update.duration",
            "s",
            "Duration of an update of the index of states, lock wait included.",
            tags: null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = UpdateBuckets });
        versionsIndexed = meter.CreateCounter<long>(
            "lodb.states.versions.indexed", "{version}", "Versions added to the index.");
        opened = meter.CreateCounter<long>(
            "lodb.states.opened", "{state}", "States opened: new, changed or returned.");
        closed = meter.CreateCounter<long>(
            "lodb.states.closed", "{state}", "States closed by a change or a retirement.");
        copyLoads = meter.CreateCounter<long>(
            "lodb.states.copy.loads",
            "{load}",
            "Loads of the copy of the index a process holds: loaded or failed.");
        unavailable = meter.CreateCounter<long>(
            "lodb.states.unavailable",
            "{read}",
            "Reads of the index answered unavailable: no copy held, and its load failed.");
    }

    public static string TriggerOf(StateIndexTrigger trigger) => trigger switch
    {
        StateIndexTrigger.Promotion => PromotionTrigger,
        StateIndexTrigger.Reconcile => ReconcileTrigger,
        _ => BuildTrigger,
    };

    public static string OutcomeOf(StateIndexOutcome outcome) => outcome switch
    {
        StateIndexOutcome.Extended => ExtendedOutcome,
        StateIndexOutcome.Built => BuiltOutcome,
        StateIndexOutcome.Locked => LockedOutcome,
        _ => UnchangedOutcome,
    };

    /// <param name="trigger">What asked for the update.</param>
    /// <param name="outcome">An outcome constant, <see cref="FailedOutcome"/> included.</param>
    /// <param name="elapsed">From the call to its end.</param>
    public void RecordUpdate(StateIndexTrigger trigger, string outcome, TimeSpan elapsed)
    {
        var triggerTag = new KeyValuePair<string, object?>(TriggerTag, TriggerOf(trigger));
        updates.Add(1, triggerTag, new KeyValuePair<string, object?>(OutcomeTag, outcome));
        updateDuration.Record(elapsed.TotalSeconds, triggerTag);
    }

    /// <summary>A version indexed: the states it opened, by kind, and those it closed.</summary>
    public void RecordVersion(
        StateIndexTrigger trigger,
        IReadOnlyCollection<EntityChangeKind> openings,
        int closings)
    {
        ArgumentNullException.ThrowIfNull(openings);
        versionsIndexed.Add(1, new KeyValuePair<string, object?>(TriggerTag, TriggerOf(trigger)));
        foreach (var kind in openings.GroupBy(static kind => kind))
        {
            var tag = new KeyValuePair<string, object?>(KindTag, KindOf(kind.Key));
            opened.Add(kind.Count(), tag);
        }

        if (closings > 0)
        {
            closed.Add(closings);
        }
    }

    public void RecordCopyLoaded() =>
        copyLoads.Add(1, new KeyValuePair<string, object?>(OutcomeTag, LoadedOutcome));

    public void RecordCopyFailed() =>
        copyLoads.Add(1, new KeyValuePair<string, object?>(OutcomeTag, FailedOutcome));

    public void RecordUnavailable() => unavailable.Add(1);

    private static string KindOf(EntityChangeKind kind) => kind switch
    {
        EntityChangeKind.Changed => ChangedKind,
        EntityChangeKind.Returned => ReturnedKind,
        _ => NewKind,
    };
}
