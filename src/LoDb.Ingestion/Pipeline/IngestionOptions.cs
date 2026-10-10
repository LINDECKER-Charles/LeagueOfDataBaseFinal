namespace LoDb.Ingestion.Pipeline;

/// <summary>
/// Settings of the ingestion (<c>LoDb:Ingestion</c>), checked when the host starts.
/// </summary>
/// <remarks>
/// The parallelism of the image fetches is the egress's, <c>LoDb:Egress:FetchConcurrency</c>.
/// </remarks>
public sealed class IngestionOptions
{
    public const string SectionName = "LoDb:Ingestion";

    private const int DefaultWatchMinutes = 10;
    private const int DefaultMaxAttempts = 5;
    private const int DefaultRetryMinutes = 10;
    private const int DefaultLanguageConcurrency = 4;
    private const int DefaultRecordBatchSize = 64;
    private const int DefaultSyncConcurrency = 4;
    private const int DefaultQueueCapacity = 256;
    private const int DefaultCrawlerRequestsPerMinute = 30;
    private const int DefaultColdDatasetsPerMinute = 6;
    private const int DefaultColdImagesPerMinute = 1500;
    private const int DefaultMaxWaitingIngestions = 32;
    private const int DefaultSlotWaitSeconds = 3;

    /// <summary>Period of the patch watch (ADR 0003).</summary>
    public TimeSpan WatchPeriod { get; set; } = TimeSpan.FromMinutes(DefaultWatchMinutes);

    /// <summary>Attempts at a version before it is marked <c>failed</c>.</summary>
    public int MaxAttempts { get; set; } = DefaultMaxAttempts;

    /// <summary>
    /// Wait after a first failed attempt; it doubles with every attempt, six hours at most.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(DefaultRetryMinutes);

    /// <summary>Languages whose datasets are read at once.</summary>
    public int LanguageConcurrency { get; set; } = DefaultLanguageConcurrency;

    /// <summary>Image verdicts recorded per statement: what a run cut short keeps.</summary>
    public int RecordBatchSize { get; set; } = DefaultRecordBatchSize;

    /// <summary>On-demand ingestions running at once on an instance.</summary>
    public int SyncConcurrency { get; set; } = DefaultSyncConcurrency;

    /// <summary>On-demand requests waiting for the worker; more are refused.</summary>
    public int QueueCapacity { get; set; } = DefaultQueueCapacity;

    /// <summary>
    /// Queued requests a crawler may add per minute (C5); zero lets crawlers trigger none.
    /// </summary>
    public int CrawlerRequestsPerMinute { get; set; } = DefaultCrawlerRequestsPerMinute;

    /// <summary>
    /// (version, language) pairs a visitor's read may ingest per minute while it waits; zero
    /// lets visitors ingest none. A version already ingested costs nothing.
    /// </summary>
    public int ColdDatasetsPerMinute { get; set; } = DefaultColdDatasetsPerMinute;

    /// <summary>
    /// Images a visitor's read may fetch per minute while it waits; the others stay
    /// placeholders until a later read.
    /// </summary>
    public int ColdImagesPerMinute { get; set; } = DefaultColdImagesPerMinute;

    /// <summary>
    /// A visitor's ingestions waiting for one of the <see cref="SyncConcurrency"/> slots;
    /// more are refused at once.
    /// </summary>
    public int MaxWaitingIngestions { get; set; } = DefaultMaxWaitingIngestions;

    /// <summary>
    /// Longest wait of a visitor's ingestion for a slot. It stays under the renderer's own
    /// timeout on the API: work started for a request already given up would be lost.
    /// </summary>
    public TimeSpan SlotWait { get; set; } = TimeSpan.FromSeconds(DefaultSlotWaitSeconds);
}
