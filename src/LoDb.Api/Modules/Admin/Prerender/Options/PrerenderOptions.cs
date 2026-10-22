using LoDb.Infrastructure.PageStore;

namespace LoDb.Api.Modules.Admin.Prerender.Options;

/// <summary>
/// The pre-rendering pass (<c>LoDb:Prerender</c>, ADR 0011): where it asks the SSR for its
/// pages, for which public host, at what pace (fixed, or adaptive: <see cref="Pace"/>), and
/// which epoch nginx serves.
/// </summary>
/// <remarks>
/// The same section holds the page store's own keys (<see cref="PageStoreOptions"/>). The pass
/// stays dormant until <see cref="IsConfigured"/> and the store's keys are all set: the base
/// compose file gives the SSR's address and the served epoch, the hosts' overlay the public
/// host, the owner the writer key.
/// </remarks>
internal sealed class PrerenderOptions
{
    public const string SectionName = PageStoreOptions.SectionName;

    /// <summary>The fixed pace in production (<see cref="RendersPerMinute"/>).</summary>
    public const int DefaultRendersPerMinute = 120;

    /// <summary>
    /// A hundred renders a second: more than a rented fleet of renderers would sustain, and a
    /// bound on a typo that would make the pass spend the lane's burst and wait each refusal.
    /// </summary>
    public const int MostRendersPerMinute = 6000;

    /// <summary>
    /// Pages of a unit in flight at once by default: one, until the hosts set the SSR's lane
    /// maximum.
    /// </summary>
    public const int DefaultMaxInFlight = 1;

    /// <summary>The first epoch a pass writes; epoch 0 is reserved for the store probes.</summary>
    public const int FirstEpoch = 1;

    /// <summary>
    /// The host every page is rendered for (<c>league-of-data-base.com</c>,
    /// <c>test.league-of-data-base.com</c>), sent as <c>X-Forwarded-Host</c>: the canonical
    /// and <c>hreflang</c> links of a stored page are built from it. The SSR must list it in
    /// <c>LODB_ALLOWED_HOSTS</c>.
    /// </summary>
    public string? PublicHost { get; set; }

    /// <summary>
    /// The pass's own SSR server on the stack network
    /// (<c>http://lodb-web-ssr-prerender:4000</c>), at the host's lowest CPU weight: never the
    /// visitors' <c>lodb-web-ssr</c>, which the pass slows from its first render in flight, and
    /// never nginx, which would clear the pass's header, bound it per address and fill its own
    /// cache.
    /// </summary>
    public string? SsrBaseUrl { get; set; }

    /// <summary>
    /// The epoch nginx serves (<c>LODB_ARCHIVE_SERVE_EPOCH</c>): the one a pass opened by a
    /// promotion writes, and the one the admin shows.
    /// </summary>
    public int ServedEpoch { get; set; } = FirstEpoch;

    /// <summary>
    /// Whether the patch that leaves the latest rank joins the pass by itself: true in
    /// production, false on the staging, which stays almost empty.
    /// </summary>
    public bool AutoEnqueueOnPromotion { get; set; }

    /// <summary>
    /// The fixed pace: the renders the pass starts per minute when the regulation is off
    /// (<see cref="PaceMode.Fixed"/>) or when no regulator drives it (the command line). At
    /// most the ceiling (<see cref="PaceOptions.MaxRendersPerMinute"/>), the budget of the
    /// pass's SSR lane, so that the pass keeps within it rather than spend its burst and wait
    /// out each refusal's <c>Retry-After</c>.
    /// </summary>
    public int RendersPerMinute { get; set; } = DefaultRendersPerMinute;

    /// <summary>
    /// The most pages of a unit rendered and written at once, within the pace above. A page
    /// waits on the render, then on the store and the database: alone, the pass would never
    /// reach its pace. The window follows the pace under this bound (<c>InFlightWidth</c>), and
    /// the renderers' lanes, spread over, admit it (compose.yaml).
    /// </summary>
    public int MaxInFlight { get; set; } = DefaultMaxInFlight;

    /// <summary>
    /// The renderers behind <see cref="SsrBaseUrl"/>: the replicas of web-ssr-prerender
    /// (<c>LODB_PRERENDER_RENDERERS</c>), which share its name on the stack network. Above one,
    /// the pass resolves the name and spreads its requests over every address, the least busy
    /// first (<c>RendererSpreadHandler</c>), and the admin reads each one's load. One keeps the
    /// name as it is.
    /// </summary>
    public int Renderers { get; set; } = 1;

    /// <summary>Whether the pass spreads its renders over several renderers.</summary>
    public bool SpreadsRenders => Renderers > 1;

    /// <summary>The adaptive pace (<c>LoDb:Prerender:Pace</c>).</summary>
    public PaceOptions Pace { get; set; } = new();

    /// <summary>Whether the pass knows where to render and for which host.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicHost) && !string.IsNullOrWhiteSpace(SsrBaseUrl);
}
