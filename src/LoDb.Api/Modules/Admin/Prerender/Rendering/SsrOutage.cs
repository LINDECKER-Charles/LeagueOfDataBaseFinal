using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering;

/// <summary>
/// Whether the SSR is out of reach for the pass, said once when an outage starts and once when
/// it ends, whatever the number of pages that wait on it meanwhile.
/// </summary>
/// <remarks>
/// A page waits out an SSR out of reach without counting it (a redeploy must not fail a
/// unit), so the outage counts no page and moves no metric: these two lines are its only sign.
/// One that does not end (a wrong <see cref="PrerenderOptions.SsrBaseUrl"/>, the SSR left
/// down) shows as a start with no end. Shared by the pages in flight of the pass.
/// </remarks>
internal sealed class SsrOutage(
    IOptions<PrerenderOptions> options,
    TimeProvider clock,
    ILogger<SsrOutage> logger)
{
    private readonly Lock _gate = new();
    private long? _since;
    private DateTimeOffset? _sinceAt;

    /// <summary>Since when the SSR is out of reach (wall clock); null while it answers.</summary>
    public DateTimeOffset? Since
    {
        get
        {
            lock (_gate)
            {
                return _sinceAt;
            }
        }
    }

    /// <summary>The SSR could not be reached: starts an outage, unless one runs.</summary>
    public void Begin()
    {
        lock (_gate)
        {
            if (_since is not null)
            {
                return;
            }

            (_since, _sinceAt) = (clock.GetTimestamp(), clock.GetUtcNow());
        }

        PrerenderLog.SsrUnreachable(logger, options.Value.SsrBaseUrl ?? string.Empty);
    }

    /// <summary>The SSR answered: ends the outage, if one ran.</summary>
    public void End()
    {
        long since;
        lock (_gate)
        {
            if (_since is not { } started)
            {
                return;
            }

            (since, _since, _sinceAt) = (started, null, null);
        }

        var seconds = (long)clock.GetElapsedTime(since).TotalSeconds;
        PrerenderLog.SsrReachable(logger, options.Value.SsrBaseUrl ?? string.Empty, seconds);
    }
}
