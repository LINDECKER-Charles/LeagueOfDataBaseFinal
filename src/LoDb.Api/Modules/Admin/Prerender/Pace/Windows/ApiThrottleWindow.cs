using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

/// <summary>
/// The API container's throttled time over the last minute, as a share of the wall time:
/// the pass's own cost on the API, which <c>ConteneurCpuBride</c> alerts on at 20 %.
/// </summary>
internal sealed class ApiThrottleWindow(TimeProvider clock)
{
    private readonly SampleRing<(long At, CgroupCpuSample Sample)> _samples =
        new(PaceTiming.ShortWindowSamples + 1);

    public void Add(CgroupCpuSample sample) => _samples.Add((clock.GetTimestamp(), sample));

    /// <summary>
    /// The cores the API used over the last minute; null before two samples or when a counter
    /// went back.
    /// </summary>
    public double? Cores()
    {
        if (_samples.Span(PaceTiming.ShortWindowSamples) is not var (earlier, latest))
        {
            return null;
        }

        var wall = clock.GetElapsedTime(earlier.At, latest.At).TotalMicroseconds;
        var used = latest.Sample.UsageMicroseconds - earlier.Sample.UsageMicroseconds;
        return wall > 0 && used >= 0 ? used / wall : null;
    }

    /// <summary>The throttled share; null before two samples or when a counter went back.</summary>
    public double? Share()
    {
        if (_samples.Span(PaceTiming.ShortWindowSamples) is not var (earlier, latest))
        {
            return null;
        }

        var wall = clock.GetElapsedTime(earlier.At, latest.At).TotalMicroseconds;
        var throttled = latest.Sample.ThrottledMicroseconds - earlier.Sample.ThrottledMicroseconds;
        return wall > 0 && throttled >= 0 ? throttled / wall : null;
    }
}
