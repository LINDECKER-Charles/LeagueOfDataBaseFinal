namespace LoDb.Infrastructure.Audience;

/// <summary>
/// The audience Umami counts, as the API pulls it for <c>/v1/trends</c> (<c>LoDb:Audience</c>),
/// checked when the host starts.
/// </summary>
/// <remarks>
/// <para>
/// The pull stays dormant until <see cref="Host"/>, <see cref="WebsiteId"/>,
/// <see cref="Username"/> and <see cref="Password"/> are all set: production's overlay
/// (compose.umami.yaml) gives the first two, the owner the read-only account (secret files).
/// </para>
/// <para>
/// Umami's collection endpoint is public, so its figures can be inflated: the trends read an
/// entity's day at most <see cref="ClipFactor"/> times its median over the
/// <see cref="ClipWindowDays"/> days that end with it.
/// </para>
/// </remarks>
public sealed class AudienceOptions
{
    public const string SectionName = "LoDb:Audience";

    private const int DefaultPullHours = 1;
    private const int DefaultRefreshDays = 2;
    private const int DefaultPageSize = 500;
    private const int DefaultMaxPages = 40;
    private const int DefaultRequestTimeoutSeconds = 30;
    private const int DefaultMaxResponseBytes = 4 * 1024 * 1024;
    private const double DefaultClipFactor = 3;
    private const int DefaultClipWindowDays = 28;

    /// <summary>
    /// Host of the Umami that counts the site, reached in https only and alone: the site's
    /// dashboard (<c>stats.league-of-data-base.com</c>).
    /// </summary>
    public string? Host { get; set; }

    /// <summary>The site in Umami, the id compose.umami.yaml declares.</summary>
    public Guid? WebsiteId { get; set; }

    /// <summary>A read-only Umami account, member of the site's team.</summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>Time between two pulls.</summary>
    public TimeSpan PullPeriod { get; set; } = TimeSpan.FromHours(DefaultPullHours);

    /// <summary>
    /// UTC days each pull rewrites, today included: yesterday's count closes after midnight.
    /// </summary>
    public int RefreshDays { get; set; } = DefaultRefreshDays;

    /// <summary>Pages of a day asked to Umami at once, the most viewed first.</summary>
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Requests per day at most: pages beyond, the least viewed, are not pulled.</summary>
    public int MaxPages { get; set; } = DefaultMaxPages;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(DefaultRequestTimeoutSeconds);

    /// <summary>Largest answer of Umami read; a larger one fails the pull.</summary>
    public int MaxResponseBytes { get; set; } = DefaultMaxResponseBytes;

    /// <summary>
    /// First UTC day the trends read from Umami's figures, the in-house measure before it.
    /// Unset, the trends keep reading the in-house measure alone.
    /// </summary>
    public DateOnly? TrendsSwitchDate { get; set; }

    /// <summary>Most an entity's day counts, in times its median (see the remarks).</summary>
    public double ClipFactor { get; set; } = DefaultClipFactor;

    /// <summary>Days, the counted one included, whose median bounds a day.</summary>
    public int ClipWindowDays { get; set; } = DefaultClipWindowDays;

    /// <summary>Whether the pull has all it needs to run.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && WebsiteId is not null
        && !string.IsNullOrEmpty(Username)
        && !string.IsNullOrEmpty(Password);
}
