using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// The patch page (<c>/{locale}/{version}/changes</c>) in the queue: it enters under its own
/// name, alone (nothing lies below it), and a pass holds it only through a unit over every
/// type, the page having none.
/// </summary>
public sealed class DemandPatchPageTests(PostgresContainerFixture postgres)
{
    private static readonly string PatchPage = DemandRig.PathOf("changes");

    // The six pages of the fake catalog, the patch page after the lists as VersionPages puts it.
    private static readonly IReadOnlyList<string> Pages =
    [
        "champions", "items", "runes", "summoners", "changes", "champions/Aatrox",
        "items/1036-long-sword",
    ];

    [Fact]
    public async Task PatchPageEntersTheQueueAloneUnderItsOwnName()
    {
        await using var rig = await StartAsync();
        rig.Refused.Refuse(PatchPage, 2);
        rig.Refused.Refuse(DemandRig.PathOf("changes/items"));

        await rig.CollectAsync();

        var row = Assert.Single(await rig.RowsAsync());
        Assert.Equal(
            (PatchPage, "changes", 2L, PrerenderDemandState.Pending),
            (row.Path, row.PageType, row.Demand, row.State));
    }

    [Fact]
    public async Task PatchPageIsWrittenByTheQueueAfterAPassOverOneType()
    {
        await using var rig = await StartAsync();
        await rig.Harness.LaunchAsync(PassScope.Of(DemandRig.Version) with
        {
            Locales = [UiLocale.En],
            PageTypes = [ResourceType.Items],
        });
        await rig.Harness.RunAsync();
        rig.Refused.Refuse(PatchPage);
        await rig.CollectAsync();

        await rig.DrainAsync();

        Assert.Equal(PrerenderDemandState.Done, (await rig.RowAsync(PatchPage))!.State);
        Assert.True(rig.Harness.Store!.Objects.ContainsKey($"pages/1{PatchPage}"));
    }

    [Fact]
    public async Task PatchPageAPassOverEveryTypeWroteEntersSkipped()
    {
        await using var rig = await StartAsync();
        await rig.Harness.LaunchAsync(PassScope.Of(DemandRig.Version) with
        {
            Locales = [UiLocale.En],
        });
        await rig.Harness.RunAsync();
        rig.Refused.Refuse(PatchPage);

        await rig.CollectAsync();

        var row = await rig.RowAsync(PatchPage);
        Assert.Equal(
            (PrerenderDemandState.Skipped, ServedUnits.AlreadyStored),
            (row!.State, row.Reason));
    }

    private async Task<DemandRig> StartAsync()
    {
        var rig = await DemandRig.StartAsync(postgres);
        rig.Harness.Catalog.Pages[DemandRig.Version] = VersionPageList.Ready(Pages);
        return rig;
    }
}
