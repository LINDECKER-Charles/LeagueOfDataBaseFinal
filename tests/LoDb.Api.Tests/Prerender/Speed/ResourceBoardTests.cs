using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;
using LoDb.Api.Modules.Admin.Prerender.Pace.Resources;
using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;
using Microsoft.Extensions.Time.Testing;

using LoDb.Api.Tests.Prerender.Pace;

namespace LoDb.Api.Tests.Prerender.Speed;

/// <summary>
/// The resources view: the renderers' cores and renders summed over every replica, a restarted
/// one counting again from nothing; their CPU per page once enough pages were rendered; the
/// visitors' refusals since the read before.
/// </summary>
public sealed class ResourceBoardTests
{
    private static readonly HostShares Host = new(0.4, 0.15, 0.3);
    private static readonly StackCpu Stack = new(4, 0.1);

    [Fact]
    public void RestartedServerCountsAgainFromNothing()
    {
        var tally = new CpuTally();

        tally.Add("a", 10, 100);
        tally.Add("a", 13, 130);
        tally.Add("b", 5, 50);
        tally.Add("b", 7, 70);
        tally.Add("a", 1, 10);
        tally.Add("a", 2, 20);

        Assert.Equal((6d, 60L), (tally.CpuSeconds, tally.Renders));
    }

    [Fact]
    public void RenderersAreSummedAndTheirCostMeasuredOnceEnoughPagesRendered()
    {
        var clock = new FakeTimeProvider();
        var board = new ResourceBoard(clock);
        board.Note(Set(Renderer(0, 0), Renderer(0, 0)), Host, Stack);

        clock.Advance(TimeSpan.FromSeconds(30));
        board.Note(Set(Renderer(3, 20), Renderer(3, 20)), Host, Stack);
        var few = board.Readings!;
        clock.Advance(TimeSpan.FromSeconds(30));
        board.Note(Set(Renderer(6, 60), Renderer(6, 60)), Host, Stack);

        var read = board.Readings!;
        Assert.Null(few.RendererCpuSecondsPerPage);
        Assert.Equal(2, read.Renderers);
        Assert.Equal(0.2, read.RenderersCores!.Value, 6);
        Assert.Equal(120, read.RenderersPerMinute!.Value, 6);
        Assert.Equal(0.1, read.RendererCpuSecondsPerPage!.Value, 6);
        Assert.Equal((4, 0.4, 0.1), (read.HostCpus, read.HostBusy, read.ApiCores));
    }

    [Fact]
    public void VisitorsRefusalsAreCountedSinceTheReadBefore()
    {
        var clock = new FakeTimeProvider();
        var board = new ResourceBoard(clock);
        board.Note(new SsrLoadSet(Visitors(refused: 1, older: 100), Empty), Host, Stack);
        var first = board.Readings!;

        clock.Advance(TimeSpan.FromSeconds(30));
        board.Note(new SsrLoadSet(Visitors(refused: 3, older: 180), Empty), Host, Stack);

        var read = board.Readings!;
        Assert.Null(first.VisitorsRefused);
        Assert.Equal(2, read.VisitorsRefused);
        Assert.Equal(160, read.OlderRefusedPerMinute!.Value, 6);
    }

    private static IReadOnlyDictionary<string, SsrLoad> Empty { get; } =
        new Dictionary<string, SsrLoad>();

    private static SsrLoadSet Set(SsrLoad first, SsrLoad second) => new(
        FakePaceSources.LoadOf(0, 0),
        new Dictionary<string, SsrLoad> { ["a"] = first, ["b"] = second });

    /// <summary>A renderer that used <paramref name="cpu"/> s for <paramref name="renders"/>.
    /// </summary>
    internal static SsrLoad Renderer(double cpu, long renders)
    {
        var load = FakePaceSources.LoadOf(0, 0);
        return load with
        {
            CpuSeconds = cpu,
            Lanes = load.Lanes with
            {
                Prerender = load.Lanes.Prerender with { Renders = renders },
            },
        };
    }

    private static SsrLoad Visitors(long refused, long older)
    {
        var load = FakePaceSources.LoadOf(10, refused);
        return load with
        {
            Lanes = load.Lanes with { Pinned = load.Lanes.Pinned with { Refused = older } },
        };
    }
}
