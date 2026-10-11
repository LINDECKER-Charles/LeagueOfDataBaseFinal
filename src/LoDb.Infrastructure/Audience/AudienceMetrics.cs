using System.Diagnostics.Metrics;

namespace LoDb.Infrastructure.Audience;

/// <summary>
/// Meter <c>LoDb.Audience</c>: the page views Umami counted on the site during the last closed
/// hour, as the last pull read them (<c>lodb_audience_vues_tirees</c>).
/// </summary>
/// <remarks>
/// The sentinel of the whole chain (emitter, edge, Umami, pull): a collapse against its weekly
/// median says the audience is lost somewhere. No series until a first pull succeeds, so a
/// dormant pull alerts nothing. Named <c>lodb_</c>, never <c>umami_</c>: those belong to the
/// reconciler of infra-vps.
/// </remarks>
internal sealed class AudienceMetrics
{
    public const string MeterName = "LoDb.Audience";

    private const long NotPulled = -1;

    private long _pulledViews = NotPulled;

    public AudienceMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        meter.CreateObservableGauge(
            "lodb.audience.vues_tirees",
            Observe,
            unit: "{view}",
            description: "Page views Umami counted on the site in the last closed hour.");
    }

    public void RecordPulledViews(long views) => Volatile.Write(ref _pulledViews, views);

    private IEnumerable<Measurement<long>> Observe()
    {
        var views = Volatile.Read(ref _pulledViews);
        return views == NotPulled ? [] : [new Measurement<long>(views)];
    }
}
