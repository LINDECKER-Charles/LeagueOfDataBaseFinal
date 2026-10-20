using LoDb.Infrastructure.PageStore;

namespace LoDb.Api.Modules.Admin.Prerender.Options;

/// <summary>
/// The pre-rendering pass (<c>LoDb:Prerender</c>, ADR 0011): where it asks the SSR for its
/// pages, for which public host, at what pace, and which epoch nginx serves.
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

    /// <summary>The SSR's own budget of its <c>prerender</c> lane in production.</summary>
    public const int DefaultRendersPerMinute = 120;

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
    /// The SSR server on the stack network (<c>http://lodb-web-ssr:4000</c>), never nginx,
    /// which would clear the pass's header, bound it per address and fill its own cache.
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
    /// Renders the pass starts per minute: the SSR's <c>LODB_PRERENDER_RENDERS_PER_MINUTE</c>,
    /// so that the pass keeps within the lane's budget rather than spend its burst and wait
    /// out each refusal's <c>Retry-After</c>.
    /// </summary>
    public int RendersPerMinute { get; set; } = DefaultRendersPerMinute;

    /// <summary>
    /// Pages of a unit rendered and written at once, within the pace above: at most the SSR's
    /// <c>LODB_PRERENDER_MAX_IN_FLIGHT</c>, beyond which it refuses them. A page waits on the
    /// render, then on the store and the database: alone, the pass would never reach its pace.
    /// </summary>
    public int MaxInFlight { get; set; } = DefaultMaxInFlight;

    /// <summary>Whether the pass knows where to render and for which host.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicHost) && !string.IsNullOrWhiteSpace(SsrBaseUrl);
}
