using System.Threading.Channels;
using LoDb.Api.Modules.Catalog.WarmUp;
using LoDb.Api.Modules.Catalog.WarmUp.Progress;
using LoDb.Domain.Catalog;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Tests.Catalog.WarmUp;

/// <summary>
/// The frames a run publishes: a run cut short keeps the counts it reached, so a client never
/// sees its progress fall back to nothing; a run cut before its images counts none.
/// </summary>
public sealed class WarmUpTrackerTests
{
    private const string Pending = "catalog-pending";

    private static readonly PlannedImage Ahri = Planned(ResourceType.Champions, "Ahri");
    private static readonly PlannedImage Garen = Planned(ResourceType.Champions, "Garen");
    private static readonly PlannedImage Boots =
        new(ResourceType.Items, new DdragonImage(DdragonImageKind.Item, "1001.png"), "Boots");

    private readonly Channel<WarmUpProgress> frames = Channel.CreateUnbounded<WarmUpProgress>();

    [Fact]
    public void RunCutShortKeepsTheCountsItReached()
    {
        var tracker = new WarmUpTracker(
            [ResourceType.Champions, ResourceType.Items],
            frames.Writer);
        tracker.Prepare();
        tracker.Start([Ahri, Garen, Boots]);
        tracker.Report(Ahri.Image);
        tracker.Report(Garen.Image);
        tracker.Complete(ResourceType.Champions);

        tracker.Fail(Pending);

        var last = Last();
        Assert.Equal((WarmUpStage.Failed, Pending), (last.Stage, last.Failure));
        Assert.Equal((3, 2), (last.Total, last.Settled));
        Assert.Equal([true, false], last.Resources.Select(static resource => resource.Ready));
        Assert.Equal("Garen", last.Latest?.Name);
    }

    [Fact]
    public void RunCutBeforeItsImagesCountsNone()
    {
        var tracker = new WarmUpTracker([ResourceType.Items], frames.Writer);
        tracker.Prepare();

        tracker.Fail(Pending);

        var last = Last();
        Assert.Equal((WarmUpStage.Failed, 0, 0), (last.Stage, last.Total, last.Settled));
        Assert.False(Assert.Single(last.Resources).Ready);
        Assert.Null(last.Latest);
    }

    private static PlannedImage Planned(ResourceType resource, string name) =>
        new(resource, new DdragonImage(DdragonImageKind.Champion, $"{name}.png"), name);

    private WarmUpProgress Last()
    {
        WarmUpProgress? last = null;
        while (frames.Reader.TryRead(out var frame))
        {
            last = frame;
        }

        return last ?? throw new InvalidOperationException("No frame was published.");
    }
}
