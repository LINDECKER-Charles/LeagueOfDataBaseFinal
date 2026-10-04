namespace LoDb.Ingestion.Egress;

/// <summary>
/// Settings of the filtered outbound client, section <c>LoDb:Egress</c>.
/// </summary>
public sealed class EgressOptions
{
    public const string SectionName = "LoDb:Egress";

    private const int DefaultFetchConcurrency = 16;
    private const int DefaultAttemptTimeoutSeconds = 15;
    private const int DefaultMaxRetryAttempts = 3;
    private const int DefaultMaxRedirects = 10;
    private const long DefaultMaxResponseBytes = 32L * 1024 * 1024;

    /// <summary>
    /// Hosts used when the configuration does not list any: Data Dragon and CommunityDragon.
    /// </summary>
    public static IReadOnlyList<string> DefaultAllowedHosts { get; } =
        ["ddragon.leagueoflegends.com", "raw.communitydragon.org"];

    /// <summary>
    /// The only hosts the client may reach, over https. An explicitly empty list refuses to
    /// start: it would leave the service healthy while every fetch fails.
    /// </summary>
    public IList<string> AllowedHosts { get; } = [];

    /// <summary>
    /// Parallel fetches of the ingestion, also the connection cap per server: the image CDN
    /// only speaks HTTP/1.1, so each in-flight fetch holds its own connection.
    /// </summary>
    public int FetchConcurrency { get; set; } = DefaultFetchConcurrency;

    public TimeSpan AttemptTimeout { get; set; } =
        TimeSpan.FromSeconds(DefaultAttemptTimeoutSeconds);

    public int MaxRetryAttempts { get; set; } = DefaultMaxRetryAttempts;

    /// <summary>
    /// First delay of the exponential backoff between two attempts.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    public int MaxRedirects { get; set; } = DefaultMaxRedirects;

    /// <summary>
    /// Cap of one response body. championFull.json, the largest legitimate file, weighs a few
    /// MB: the cap leaves headroom while keeping a hostile upstream from growing the heap.
    /// </summary>
    public long MaxResponseBytes { get; set; } = DefaultMaxResponseBytes;
}
