using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;
using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// The shares the samples give: the host busy and stolen over a minute, used over 30; the
/// API's throttled share; the pass's median and starts over the last minute.
/// </summary>
public sealed class PaceWindowsTests
{
    private const long TicksPerSample = 1000;

    [Fact]
    public void HostSharesReadTheLastMinuteAndTheLastHalfHour()
    {
        var window = new HostWindow();
        var (idle, steal, total) = (0L, 0L, 0L);
        for (var sample = 0; sample <= PaceTiming.LongWindowSamples; sample++)
        {
            // Calm for 24 minutes (busy 30 %, no steal), then busy 70 % with 20 % stolen.
            var isLate = sample > 144;
            (idle, steal, total) = (
                idle + (isLate ? 300 : 700),
                steal + (isLate ? 200 : 0),
                total + TicksPerSample);
            window.Add(new HostCpuSample(idle, steal, total));
        }

        var shares = window.Shares();

        Assert.Equal(0.7, shares.Busy!.Value, 6);
        Assert.Equal(0.2, shares.Steal!.Value, 6);
        Assert.Equal(((0.3 * 144) + (0.5 * 36)) / 180, shares.Used!.Value, 6);
    }

    [Fact]
    public void HostSharesNeedTwoSamplesAndCountersThatMoveForward()
    {
        var window = new HostWindow();
        window.Add(new HostCpuSample(500, 0, 1000));
        var single = window.Shares();
        window.Add(new HostCpuSample(10, 0, 20));

        Assert.Equal(HostShares.Unknown, single);
        Assert.Equal(HostShares.Unknown, window.Shares());
    }

    [Fact]
    public void ApiThrottledShareIsOverTheWallTime()
    {
        var clock = new FakeTimeProvider();
        var window = new ApiThrottleWindow(clock);
        for (var sample = 0; sample <= PaceTiming.ShortWindowSamples; sample++)
        {
            window.Add(new CgroupCpuSample(sample * 1_000_000L, sample * 500_000L));
            clock.Advance(PaceTiming.SamplePeriod);
        }

        Assert.Equal(0.05, window.Share()!.Value, 6);
    }

    [Fact]
    public void PassRendersAreMeasuredOverTheLastMinute()
    {
        var clock = new FakeTimeProvider();
        var renders = new PassRenderTimes(clock);
        renders.Rendered(TimeSpan.FromSeconds(9));
        renders.Started();
        clock.Advance(TimeSpan.FromSeconds(61));
        foreach (var seconds in new[] { 0.4, 0.1, 0.3, 0.2 })
        {
            renders.Rendered(TimeSpan.FromSeconds(seconds));
            renders.Started();
        }

        var stats = renders.Measure();

        Assert.Equal((4, 4), (stats.Renders, stats.Starts));
        Assert.Equal(0.25, stats.MedianSeconds!.Value, 6);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(new PassRenderStats(0, null, 0), renders.Measure());
    }

    [Fact]
    public void BaselineIsTheLowestMedianOfTheLastHalfHour()
    {
        var baseline = new PassLatencyBaseline();
        var start = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

        baseline.Note(start, 0.1);
        baseline.Note(start.AddMinutes(10), 0.3);
        var early = baseline.Lowest();
        baseline.Note(start.AddMinutes(31), 0.4);

        Assert.Equal((0.1, 0.3), (early!.Value, baseline.Lowest()!.Value));
    }
}
