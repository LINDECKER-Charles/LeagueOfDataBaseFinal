using LoDb.Api.Modules.Admin.Prerender.Demand;
using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// The SSR's refused pages enter the queue once each, with the increase of their cumulative
/// count: never counted twice, whatever restarts on either side; junk never enters, and a page
/// a pass stored in the served epoch enters skipped.
/// </summary>
public sealed class DemandCaptureTests(PostgresContainerFixture postgres)
{
    private static readonly string Aatrox = DemandRig.PathOf("champions/Aatrox");
    private static readonly string Sword = DemandRig.PathOf("items/1036-long-sword");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RefusedPagesEnterPendingWithTheirCount()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox, 3);
        rig.Refused.Refuse(DemandRig.PathOf("runes", "fr"));

        await rig.CollectAsync();

        var rows = await rig.RowsAsync();
        Assert.Equal(
            [("/en/16.11.1/champions/Aatrox", 3L, "champions"), ("/fr/16.11.1/runes", 1L, "runes")],
            rows.Select(static row => (row.Path, row.Demand, row.PageType)));
        Assert.All(rows, static row => Assert.Equal(PrerenderDemandState.Pending, row.State));
        Assert.Equal(("16.11.1", "fr"), (rows[1].Version, rows[1].Locale));
    }

    [Fact]
    public async Task OnlyTheIncreaseIsAddedAndAReadMadeTwiceAddsNothing()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox, 3);
        await rig.CollectAsync();

        rig.Refused.Refuse(Aatrox, 2);
        await rig.CollectAsync();
        await rig.CollectAsync();

        Assert.Equal(5, (await rig.RowAsync(Aatrox))!.Demand);
        Assert.Equal([0L, 1L, 2L], rig.Refused.Reads);
    }

    [Fact]
    public async Task RestartOfTheApiReadsEverythingAgainWithoutCountingItTwice()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox, 4);
        await rig.CollectAsync();

        await rig.NewCollector().CollectAsync(Token);

        Assert.Equal(4, (await rig.RowAsync(Aatrox))!.Demand);
    }

    [Fact]
    public async Task RestartOfTheSsrCountsItsNewRefusalsFromZero()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox, 4);
        rig.Refused.Refuse(Sword, 1);
        await rig.CollectAsync();

        rig.Refused.Restart();
        rig.Refused.Refuse(Aatrox, 1);
        await rig.CollectAsync();

        Assert.Equal(5, (await rig.RowAsync(Aatrox))!.Demand);
        Assert.Equal(1, (await rig.RowAsync(Sword))!.Demand);
        Assert.Equal(0, rig.Refused.Reads[^1]);
    }

    [Fact]
    public async Task LongBacklogIsReadInSeveralAnswersFromTheCursor()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Limit = 2;
        foreach (var page in FakeCatalogPages())
        {
            rig.Refused.Refuse(DemandRig.PathOf(page));
        }

        await rig.CollectAsync();

        Assert.Equal(6, (await rig.RowsAsync()).Count);
        Assert.Equal([0L, 2L, 4L], rig.Refused.Reads);
    }

    [Theory]
    [InlineData("/en/16.11.1/items/1036-short-sword")]
    [InlineData("/en/16.11.1/items/9999-long-sword")]
    [InlineData("/en/16.11.1/champions/aatrox")]
    [InlineData("/en/16.20.1/champions")]
    [InlineData("/en/16.11.1/patches")]
    [InlineData("/xx/16.11.1/champions")]
    [InlineData("/en/16.11.1/champions/Aatrox/skins")]
    [InlineData("en/16.11.1/champions")]
    public async Task PageOutsideTheCatalogueNeverEnters(string path)
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Harness.Catalog.Latest = PatchVersion.Parse("16.20.1");
        rig.Refused.Refuse(path, 50);

        await rig.CollectAsync();

        Assert.Empty(await rig.RowsAsync());
    }

    [Fact]
    public async Task PageOfAVersionNotStoredYetIsJudgedAtItsNextRefusal()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Harness.Catalog.PendingReads[DemandRig.Version] = 1;
        rig.Refused.Refuse(Aatrox, 2);
        await rig.CollectAsync();
        Assert.Empty(await rig.RowsAsync());

        rig.Refused.Refuse(Aatrox);
        await rig.CollectAsync();

        Assert.Equal(3, (await rig.RowAsync(Aatrox))!.Demand);
    }

    [Fact]
    public async Task PageAPassStoredInTheServedEpochEntersSkipped()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        await rig.Harness.LaunchAsync(PassScope.Of(DemandRig.Version) with
        {
            Locales = [Domain.Languages.UiLocale.En],
        });
        await rig.Harness.RunAsync();
        rig.Refused.Refuse(Aatrox);
        rig.Refused.Refuse(DemandRig.PathOf("champions/Aatrox", "fr"));

        await rig.CollectAsync();

        var stored = await rig.RowAsync(Aatrox);
        Assert.Equal(
            (PrerenderDemandState.Skipped, (int?)1, ServedUnits.AlreadyStored),
            (stored!.State, stored.Epoch, stored.Reason));
        Assert.Equal(
            PrerenderDemandState.Pending,
            (await rig.RowAsync(DemandRig.PathOf("champions/Aatrox", "fr")))!.State);
    }

    [Fact]
    public async Task SwitchedOffQueueNeverReadsTheSsr()
    {
        await using var rig = await DemandRig.StartAsync(
            postgres,
            new Dictionary<string, string?> { [$"{DemandOptions.SectionName}:Enabled"] = "false" });
        rig.Refused.Refuse(Aatrox);

        await rig.CollectAsync();

        Assert.Empty(rig.Refused.Reads);
        Assert.Empty(await rig.RowsAsync());
    }

    [Fact]
    public async Task SsrOutOfReachLeavesTheCursorWhereItWas()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(Aatrox);
        rig.Refused.IsDown = true;
        await rig.CollectAsync();

        rig.Refused.IsDown = false;
        await rig.CollectAsync();

        Assert.Equal([0L, 0L], rig.Refused.Reads);
        Assert.Equal(1, (await rig.RowAsync(Aatrox))!.Demand);
    }

    private static IEnumerable<string> FakeCatalogPages() => Support.FakeCatalog.SixPages;
}
