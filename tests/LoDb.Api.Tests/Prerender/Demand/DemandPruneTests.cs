using LoDb.Api.Modules.Admin.Prerender.Demand;
using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// The table stays bounded: pages no visitor was refused for the retention go, then the least
/// requested pending pages past the bound; the epoch served again revives what another left.
/// </summary>
public sealed class DemandPruneTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PagesNoLongerRefusedGoAfterTheRetention()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Refused.Refuse(DemandRig.PathOf("champions"));
        await rig.CollectAsync();
        rig.Harness.Clock.Advance(TimeSpan.FromDays(29));
        rig.Refused.Refuse(DemandRig.PathOf("items"));
        await rig.CollectAsync();

        rig.Harness.Clock.Advance(TimeSpan.FromDays(2));
        var removed = await rig.Harness.Get<DemandPruner>().PruneAsync(Token);

        Assert.Equal(1, removed);
        Assert.Equal([DemandRig.PathOf("items")], (await rig.RowsAsync()).Select(r => r.Path));
    }

    [Fact]
    public async Task LeastRequestedPendingPagesGoPastTheBound()
    {
        await using var rig = await DemandRig.StartAsync(
            postgres,
            new Dictionary<string, string?>
            {
                [$"{DemandOptions.SectionName}:MaxPendingPages"] = "100",
            });
        var locales = Domain.Languages.UiLocales.All
            .Select(Domain.Languages.UiLocales.Code)
            .ToList();
        var pages = locales.SelectMany(locale => Support.FakeCatalog.SixPages
            .Select(page => DemandRig.PathOf(page, locale)))
            .Take(105)
            .ToList();
        for (var index = 0; index < pages.Count; index++)
        {
            rig.Refused.Refuse(pages[index], index + 1);
        }

        await rig.CollectAsync();
        var removed = await rig.Harness.Get<DemandPruner>().PruneAsync(Token);

        Assert.Equal(5, removed);
        var kept = (await rig.RowsAsync()).Select(static row => row.Path).ToHashSet();
        Assert.Equal(100, kept.Count);
        Assert.DoesNotContain(pages[0], kept);
        Assert.Contains(pages[^1], kept);
    }
}
