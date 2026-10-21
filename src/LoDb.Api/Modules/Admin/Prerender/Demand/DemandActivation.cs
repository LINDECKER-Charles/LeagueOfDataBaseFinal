using LoDb.Api.Modules.Admin.Prerender.Options;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand;

/// <summary>
/// Whether the automatic queue runs here: switched on (<see cref="DemandOptions.Enabled"/>)
/// and the pre-rendering active, its store configured. Until then its workers do nothing and
/// say nothing, as the pass's.
/// </summary>
internal sealed class DemandActivation(
    PrerenderActivation prerender,
    IOptions<DemandOptions> options)
{
    public bool IsEnabled => options.Value.Enabled;

    /// <summary>Whether the queue renders the pages it holds.</summary>
    public bool IsActive => IsEnabled && prerender.IsActive;

    /// <summary>Where it reads the refused pages, while it is active; null otherwise.</summary>
    public Uri? RefusedPagesUrl => IsActive
        && Uri.TryCreate(options.Value.RefusedPagesUrl, UriKind.Absolute, out var url)
            ? url
            : null;
}
