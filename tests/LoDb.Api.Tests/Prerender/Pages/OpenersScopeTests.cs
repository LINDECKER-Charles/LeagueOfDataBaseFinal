using LoDb.Api.Modules.Admin.Prerender.Coverage;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Promotion;
using LoDb.Api.Tests.Prerender.Demand;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender.Pages;

/// <summary>
/// A pass over the openers (<c>{"pages":"openers"}</c>): each unit walks the patch page and the
/// pages that open a state at its version, in the order of the version's pages; it never stands
/// for a pass over every page (no type held, no promotion joining it), and the queue skips only
/// the pages its units wrote.
/// </summary>
public sealed class OpenersScopeTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.11.1");

    // The pages of the version, the patch page after the lists, as VersionPages puts them.
    private static readonly IReadOnlyList<string> Pages =
    [
        "champions", "items", "runes", "summoners", "changes", "champions/Aatrox",
        "champions/Ahri", "items/1036-long-sword", "items/3078-trinity-force",
    ];

    // The states opened at the version, in no order: the index holds them so.
    private static readonly IReadOnlySet<string> Openers =
        new HashSet<string>(["items/1036-long-sword", "champions/Ahri"], StringComparer.Ordinal);

    private static readonly PassScope OpenersScope = PassScope.Of(Version) with
    {
        Locales = [UiLocale.En],
        Pages = PassPages.Openers,
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void OpenersScopeIsStoredAsItsPagesAndReadBack()
    {
        var stored = PassScopeJson.Write(new PassScope { Pages = PassPages.Openers });

        Assert.Equal("{\"pages\":\"openers\"}", stored);
        Assert.Equal(PassPages.Openers, PassScopeJson.Read(stored).Pages);
        Assert.Equal(PassPages.Every, PassScopeJson.Read("{}").Pages);
        Assert.Throws<FormatException>(() => PassScopeJson.Read("{\"pages\":\"all\"}"));
    }

    [Fact]
    public void OpenersScopeIsNeitherWholeNorOverEveryTypeAndHoldsNone()
    {
        var scope = new PassScope { Pages = PassPages.Openers };

        Assert.Equal(
            (false, false, TypeMask.None),
            (scope.IsWhole, scope.HasEveryPageType, TypeMask.Of(scope)));
    }

    [Fact]
    public async Task UnitWalksThePatchPageAndTheOpenersInTheOrderOfThePages()
    {
        await using var harness = await StartAsync();

        var passId = await harness.LaunchAsync(OpenersScope);
        await harness.RunAsync();

        Assert.Equal(
            [
                "/en/16.11.1/changes",
                "/en/16.11.1/champions/Ahri",
                "/en/16.11.1/items/1036-long-sword",
            ],
            harness.Ssr.Pages);
        var pass = await harness.PassAsync(passId);
        var unit = Assert.Single(pass.Units);
        Assert.Equal(
            (PrerenderPassState.Completed, 3, (int?)3),
            (pass.State, unit.PagesTotal, unit.PagesExpected));
    }

    [Fact]
    public async Task TypeFilterKeepsItsOpenersAndLeavesThePatchPageOut()
    {
        await using var harness = await StartAsync();

        await harness.LaunchAsync(OpenersScope with { PageTypes = [ResourceType.Items] });
        await harness.RunAsync();

        Assert.Equal(["/en/16.11.1/items/1036-long-sword"], harness.Ssr.Pages);
    }

    [Fact]
    public async Task CompletedOpenersPassMakesTheEpochHoldNoTypeAndLetsNoPromotionJoin()
    {
        await using var harness = await StartAsync();
        await harness.LaunchAsync(OpenersScope);
        await harness.RunAsync();
        var running = await harness.LaunchAsync(OpenersScope);

        var held = (await harness.Get<EpochCoverage>().ReadAsync(1, Token)).HeldFor(null);
        var joined = await harness.Get<VersionEnqueuer>().EnsureAsync(Version, Token);

        Assert.Empty(held);
        Assert.Equal((running, true), (joined.PassId, joined.IsDeferred));
    }

    [Fact]
    public async Task QueueSkipsTheOpenersAnOpenersPassWroteAndRendersTheOthers()
    {
        await using var rig = await DemandRig.StartAsync(postgres);
        rig.Harness.Catalog.Pages[Version] = VersionPageList.Ready(Pages);
        rig.Harness.Catalog.Openers[Version] = Openers;
        await rig.Harness.LaunchAsync(OpenersScope);
        await rig.Harness.RunAsync();
        var opener = DemandRig.PathOf("champions/Ahri");
        var other = DemandRig.PathOf("champions/Aatrox");
        rig.Refused.Refuse(opener);
        rig.Refused.Refuse(other);

        await rig.CollectAsync();

        Assert.Equal(
            (PrerenderDemandState.Skipped, PrerenderDemandState.Pending),
            ((await rig.RowAsync(opener))!.State, (await rig.RowAsync(other))!.State));
    }

    private async Task<PrerenderHarness> StartAsync()
    {
        var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.Add(Version);
        harness.Catalog.Pages[Version] = VersionPageList.Ready(Pages);
        harness.Catalog.Openers[Version] = Openers;
        return harness;
    }
}
