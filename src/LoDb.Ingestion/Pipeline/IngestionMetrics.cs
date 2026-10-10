using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace LoDb.Ingestion.Pipeline;

/// <summary>
/// Meter <c>LoDb.Ingestion</c>: durations by stage, blobs written, absences recorded,
/// versions made ready, fetches left without a verdict, refused on-demand work, the depth
/// of the queues and the on-demand ingestions waiting for a slot.
/// </summary>
internal sealed class IngestionMetrics
{
    public const string MeterName = "LoDb.Ingestion";
    public const string StageTag = "stage";
    public const string FormatTag = "format";
    public const string TypeTag = "type";
    public const string QueueTag = "queue";
    public const string ReasonTag = "reason";

    /// <summary>A whole version: datasets, then images.</summary>
    public const string VersionStage = "version";

    public const string DatasetsStage = "datasets";
    public const string ImagesStage = "images";

    public const string SourceFormat = "source";
    public const string WebpFormat = "webp";

    private readonly Histogram<double> duration;
    private readonly Counter<long> blobsWritten;
    private readonly Counter<long> absences;
    private readonly Counter<long> versionsReady;
    private readonly Counter<long> fetchFailures;
    private readonly Counter<long> refusals;
    private readonly ConcurrentDictionary<string, Func<int>> queues = new(StringComparer.Ordinal);
    private Func<int> syncWaiting = static () => 0;

    public IngestionMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        duration = meter.CreateHistogram<double>(
            "lodb.ingestion.duration", "s", "Duration of an ingestion stage.");
        blobsWritten = meter.CreateCounter<long>(
            "lodb.ingestion.blobs.written", "{blob}", "Blobs and WebP siblings written.");
        absences = meter.CreateCounter<long>(
            "lodb.ingestion.absences", "{image}", "Images recorded absent after a 403 or a 404.");
        versionsReady = meter.CreateCounter<long>(
            "lodb.ingestion.versions.ready", "{version}", "Versions fully ingested.");
        fetchFailures = meter.CreateCounter<long>(
            "lodb.ingestion.fetch.failures",
            "{image}",
            "Images left without a verdict after a transient failure.");
        refusals = meter.CreateCounter<long>(
            "lodb.ingestion.on_demand.refused",
            "{request}",
            "On-demand work refused: queue full, budget spent or no slot in time.");
        meter.CreateObservableGauge(
            "lodb.ingestion.queue.depth",
            ObserveQueues,
            unit: "{item}",
            description: "Work waiting in an ingestion queue.");
        meter.CreateObservableGauge(
            "lodb.ingestion.sync.waiting",
            () => syncWaiting(),
            unit: "{ingestion}",
            description: "On-demand ingestions waiting for a slot.");
    }

    public void RecordDuration(string stage, TimeSpan elapsed) =>
        duration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>(StageTag, stage));

    public void RecordBlobs(int sources, int webp)
    {
        if (sources > 0)
        {
            blobsWritten.Add(sources, new KeyValuePair<string, object?>(FormatTag, SourceFormat));
        }

        if (webp > 0)
        {
            blobsWritten.Add(webp, new KeyValuePair<string, object?>(FormatTag, WebpFormat));
        }
    }

    /// <param name="type">Manifest type of the image.</param>
    public void RecordAbsence(string type) =>
        absences.Add(1, new KeyValuePair<string, object?>(TypeTag, type));

    public void RecordFetchFailures(int count)
    {
        if (count > 0)
        {
            fetchFailures.Add(count);
        }
    }

    public void RecordVersionReady() => versionsReady.Add(1);

    public void RecordRefusal(string reason) =>
        refusals.Add(1, new KeyValuePair<string, object?>(ReasonTag, reason));

    /// <summary>Publishes the depth of a queue under the <c>queue</c> tag.</summary>
    public void ObserveQueue(string queue, Func<int> depth) => queues[queue] = depth;

    /// <summary>Publishes how many on-demand ingestions wait for a slot.</summary>
    public void ObserveSyncWaiting(Func<int> count) => syncWaiting = count;

    private IEnumerable<Measurement<int>> ObserveQueues() =>
        queues.Select(static entry => new Measurement<int>(
            entry.Value(),
            new KeyValuePair<string, object?>(QueueTag, entry.Key)));
}
