namespace LoDb.Api.Modules.PublicApi;

/// <summary>
/// Settings of the public API (<c>LoDb:PublicApi</c>), checked when the host starts.
/// </summary>
internal sealed class PublicApiOptions
{
    public const string SectionName = "LoDb:PublicApi";

    /// <summary>The production site, go-api's default.</summary>
    public const string DefaultSiteOrigin = "https://league-of-data-base.com";

    private const int DefaultKeyCacheSeconds = 60;
    private const int DefaultTrendsMinutes = 5;
    private const int DefaultMeteringSeconds = 1;

    /// <summary>
    /// Origin of the website the <c>share_url</c> of a build points to: the key holders sit
    /// on another origin and cannot resolve a path of the site.
    /// </summary>
    public string SiteOrigin { get; set; } = DefaultSiteOrigin;

    /// <summary>
    /// How long a key read from the database, or found unknown, is served from memory. A
    /// change made through <see cref="IApiKeyCache"/> applies at once on this instance.
    /// </summary>
    public TimeSpan KeyCacheLifetime { get; set; } = TimeSpan.FromSeconds(DefaultKeyCacheSeconds);

    /// <summary>How long a ranking of <c>/v1/trends</c> is served once computed.</summary>
    public TimeSpan TrendsLifetime { get; set; } = TimeSpan.FromMinutes(DefaultTrendsMinutes);

    /// <summary>How often the counted requests are added to <c>api_usage</c>.</summary>
    public TimeSpan MeteringInterval { get; set; } = TimeSpan.FromSeconds(DefaultMeteringSeconds);
}
