using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

/// <summary>A lane's renders between two reads of an SSR's load.</summary>
internal sealed record LaneDelta(long Renders, double RenderSeconds, long Refused)
{
    /// <summary>
    /// What happened between <paramref name="before"/> and <paramref name="now"/>: nothing to
    /// say without a read before, null when a counter went back (the SSR restarted).
    /// </summary>
    public static LaneDelta? Between(SsrLaneLoad? before, SsrLaneLoad now)
    {
        ArgumentNullException.ThrowIfNull(now);
        if (before is null)
        {
            return new LaneDelta(0, 0, 0);
        }

        var delta = new LaneDelta(
            now.Renders - before.Renders,
            now.RenderSeconds - before.RenderSeconds,
            now.Refused - before.Refused);
        return delta is { Renders: >= 0, RenderSeconds: >= 0, Refused: >= 0 } ? delta : null;
    }
}
