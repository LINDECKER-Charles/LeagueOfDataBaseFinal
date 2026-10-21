using LoDb.Api.Modules.Admin.Prerender.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>
/// How many pages of a unit may be in flight at a pace: enough for the pace to be reached,
/// never more than <see cref="PrerenderOptions.MaxInFlight"/>.
/// </summary>
/// <remarks>
/// A page in flight waits on its render (0.1 to 0.15 s measured in production, seconds for the
/// first pages of a unit, which load its catalog), its upload and its counters: a second and a
/// half each covers them. A wider window would only queue pages behind the pacer, which a pause
/// or a hold then waits out; a narrower one would cap the pace below what the operator set.
/// </remarks>
internal static class InFlightWidth
{
    /// <summary>The wall time a page in flight is budgeted, in seconds.</summary>
    public const double PageSeconds = 1.5;

    private const double SecondsPerMinute = 60;

    /// <summary>
    /// The window at <paramref name="rendersPerMinute"/>, between 1 and the most.
    /// </summary>
    public static int Of(int rendersPerMinute, int most)
    {
        var needed = (int)Math.Ceiling(rendersPerMinute * PageSeconds / SecondsPerMinute);
        return Math.Clamp(needed, 1, Math.Max(1, most));
    }
}
