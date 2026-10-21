using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

/// <summary>
/// The host's CPU time sampled every <see cref="PaceTiming.SamplePeriod"/>, over 30 minutes:
/// the shares the regulator reads (<see cref="HostShares"/>).
/// </summary>
/// <remarks>
/// <c>noeud:cpu_utilise_ratio</c> of infra-vps, the base of the hoster's throttling alert, is
/// the share used without the steal: the 30-minute rule reads the same. A window shorter than
/// asked (the API just started) reads over what it holds.
/// </remarks>
internal sealed class HostWindow
{
    private readonly SampleRing<HostCpuSample> _samples = new(PaceTiming.LongWindowSamples + 1);

    public void Add(HostCpuSample sample) => _samples.Add(sample);

    public HostShares Shares()
    {
        var minute = DeltaOver(PaceTiming.ShortWindowSamples);
        var sustained = DeltaOver(PaceTiming.LongWindowSamples);
        return new HostShares(
            minute is { } busy ? 1d - ((double)busy.Idle / busy.Total) : null,
            minute is { } stolen ? (double)stolen.Steal / stolen.Total : null,
            sustained is { } used ? 1d - ((double)(used.Idle + used.Steal) / used.Total) : null);
    }

    // Null when the counters did not move forward, as after a reboot of the host.
    private HostCpuSample? DeltaOver(int steps)
    {
        if (_samples.Span(steps) is not var (earlier, latest))
        {
            return null;
        }

        var delta = new HostCpuSample(
            latest.Idle - earlier.Idle,
            latest.Steal - earlier.Steal,
            latest.Total - earlier.Total);
        return delta is { Total: > 0, Idle: >= 0, Steal: >= 0 } ? delta : null;
    }
}
