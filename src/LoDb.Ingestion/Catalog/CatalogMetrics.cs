using System.Diagnostics.Metrics;

namespace LoDb.Ingestion.Catalog;

/// <summary>
/// Meter <c>LoDb.Catalog</c>: how the catalogs in memory answer, what loading the others
/// costs, and what the cold gate admits.
/// </summary>
/// <remarks>
/// No version, language nor caller address in a tag: the series stay few whatever a client
/// asks for. The Prometheus names are <c>lodb_catalog_*</c>, the contract of the alerts.
/// </remarks>
internal sealed class CatalogMetrics
{
    public const string MeterName = "LoDb.Catalog";
    public const string OutcomeTag = "outcome";
    public const string PartitionTag = "partition";
    public const string ResultTag = "result";

    public const string HitOutcome = "hit";
    public const string JoinedOutcome = "joined";
    public const string MissOutcome = "miss";

    public const string BuiltOutcome = "built";
    public const string MissingOutcome = "missing";
    public const string FailedOutcome = "failed";

    public const string InternalPartition = "internal";
    public const string PublicPartition = "public";
    public const string AdmittedResult = "admitted";
    public const string RefusedResult = "refused";

    // A load reads and indexes several megabytes: from a few tens of milliseconds to seconds
    // on a starved host.
    private static readonly double[] LoadBuckets = [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5];

    private readonly Counter<long> lookups;
    private readonly Counter<long> evictions;
    private readonly Histogram<double> loadDuration;
    private readonly UpDownCounter<int> loadsInFlight;
    private readonly Counter<long> admissions;

    public CatalogMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        lookups = meter.CreateCounter<long>(
            "lodb.catalog.cache.lookups", "{lookup}", "Catalog reads: hit, joined a load, miss.");
        evictions = meter.CreateCounter<long>(
            "lodb.catalog.cache.evictions", "{catalog}", "Catalogs evicted from memory.");
        loadDuration = meter.CreateHistogram(
            "lodb.catalog.load.duration",
            "s",
            "Duration of a catalog load from the stored datasets.",
            tags: null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = LoadBuckets });
        loadsInFlight = meter.CreateUpDownCounter<int>(
            "lodb.catalog.loads.in_flight", "{load}", "Catalog loads running.");
        admissions = meter.CreateCounter<long>(
            "lodb.catalog.cold.admissions", "{load}", "Catalog loads admitted or refused.");
    }

    /// <param name="outcome"><see cref="HitOutcome"/>, joined or miss.</param>
    public void RecordLookup(string outcome) =>
        lookups.Add(1, new KeyValuePair<string, object?>(OutcomeTag, outcome));

    public void RecordEviction() => evictions.Add(1);

    /// <param name="outcome"><see cref="BuiltOutcome"/>, missing or failed.</param>
    /// <param name="elapsed">From the admission to the outcome.</param>
    public void RecordLoad(string outcome, TimeSpan elapsed) =>
        loadDuration.Record(
            elapsed.TotalSeconds,
            new KeyValuePair<string, object?>(OutcomeTag, outcome));

    public void LoadStarted() => loadsInFlight.Add(1);

    public void LoadEnded() => loadsInFlight.Add(-1);

    public void RecordAdmission(ColdCaller caller, bool admitted) =>
        admissions.Add(
            1,
            new KeyValuePair<string, object?>(PartitionTag, PartitionOf(caller)),
            new KeyValuePair<string, object?>(ResultTag, ResultOf(admitted)));

    private static string PartitionOf(ColdCaller caller) =>
        caller == ColdCaller.Internal ? InternalPartition : PublicPartition;

    private static string ResultOf(bool admitted) => admitted ? AdmittedResult : RefusedResult;
}
