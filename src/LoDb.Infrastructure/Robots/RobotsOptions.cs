namespace LoDb.Infrastructure.Robots;

/// <summary>
/// The robots journal (<c>LoDb:Robots</c>), checked when the host starts: what nginx reports,
/// how it is batched, how a robot that hides is told apart, how its network is looked up and
/// how long its hits are kept.
/// </summary>
/// <remarks>
/// nginx reports every request of a robot, whatever the path, and every page request of the
/// others, which only feed <see cref="UndeclaredThreshold"/> and are never kept. A client's
/// address only serves its reverse DNS lookup: neither it nor its host name is stored.
/// </remarks>
public sealed class RobotsOptions
{
    public const string SectionName = "LoDb:Robots";

    private const int DefaultQueueCapacity = 20_000;
    private const int DefaultBatchSize = 1_000;
    private const int DefaultBatchWindowSeconds = 2;
    private const int DefaultHitRetentionDays = 30;
    private const int DefaultUndeclaredThreshold = 40;
    private const int DefaultUndeclaredWindowMinutes = 10;
    private const int DefaultUndeclaredMemoryHours = 24;
    private const int DefaultMaxTrackedClients = 100_000;
    private const int DefaultDnsTimeoutSeconds = 2;
    private const int DefaultDnsLookupsPerBatch = 32;
    private const int DefaultDnsParallelism = 8;
    private const int DefaultDnsCacheSize = 20_000;
    private const int DefaultDnsCacheHours = 6;
    private const int DefaultDnsFailureMinutes = 10;
    private const int DefaultJobStatementTimeoutMinutes = 5;

    /// <summary>Records held in memory for the writer; beyond, the next ones are dropped.</summary>
    public int QueueCapacity { get; set; } = DefaultQueueCapacity;

    /// <summary>Records turned into hits and written at once, at most.</summary>
    public int BatchSize { get; set; } = DefaultBatchSize;

    /// <summary>Wait after a first record, so that the next ones join its batch.</summary>
    public TimeSpan BatchWindow { get; set; } = TimeSpan.FromSeconds(DefaultBatchWindowSeconds);

    /// <summary>Age at which a hit is deleted; its day keeps its counts.</summary>
    public TimeSpan HitRetention { get; set; } = TimeSpan.FromDays(DefaultHitRetentionDays);

    /// <summary>
    /// Page requests of one client (address and user agent) within
    /// <see cref="UndeclaredWindow"/> past which a browser's user agent is taken for a robot
    /// that hides: a reader's next pages are rendered by the application, not asked of nginx.
    /// </summary>
    public int UndeclaredThreshold { get; set; } = DefaultUndeclaredThreshold;

    public TimeSpan UndeclaredWindow { get; set; } =
        TimeSpan.FromMinutes(DefaultUndeclaredWindowMinutes);

    /// <summary>How long a client past the threshold stays a robot.</summary>
    public TimeSpan UndeclaredMemory { get; set; } =
        TimeSpan.FromHours(DefaultUndeclaredMemoryHours);

    /// <summary>
    /// Clients whose pace is followed at once; the quietest are forgotten beyond.
    /// </summary>
    public int MaxTrackedClients { get; set; } = DefaultMaxTrackedClients;

    /// <summary>Longest wait of one DNS question.</summary>
    public TimeSpan DnsTimeout { get; set; } = TimeSpan.FromSeconds(DefaultDnsTimeoutSeconds);

    /// <summary>
    /// Addresses a batch looks up at most, those already known aside: a flood of new clients
    /// slows no batch beyond it, the others are looked up by the next batches.
    /// </summary>
    public int DnsLookupsPerBatch { get; set; } = DefaultDnsLookupsPerBatch;

    public int DnsParallelism { get; set; } = DefaultDnsParallelism;

    /// <summary>Addresses whose answer is kept in memory, at most.</summary>
    public int DnsCacheSize { get; set; } = DefaultDnsCacheSize;

    public TimeSpan DnsCacheLifetime { get; set; } = TimeSpan.FromHours(DefaultDnsCacheHours);

    /// <summary>How long a failed lookup is not tried again.</summary>
    public TimeSpan DnsFailureLifetime { get; set; } =
        TimeSpan.FromMinutes(DefaultDnsFailureMinutes);

    /// <summary>Longest statement of the daily retention, which deletes a day of hits.</summary>
    public TimeSpan JobStatementTimeout { get; set; } =
        TimeSpan.FromMinutes(DefaultJobStatementTimeoutMinutes);
}
