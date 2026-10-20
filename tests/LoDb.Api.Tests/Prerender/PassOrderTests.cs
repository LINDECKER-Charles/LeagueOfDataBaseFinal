using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender;

/// <summary>
/// The order of a pass: one (version, locale) at a time, so that each catalog loads once for
/// all its pages; the versions the operator put first, then the others newest first.
/// </summary>
public sealed class PassOrderTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Newest = PatchVersion.Parse("16.19.1");
    private static readonly PatchVersion Middle = PatchVersion.Parse("16.18.1");
    private static readonly PatchVersion Oldest = PatchVersion.Parse("16.17.1");

    [Fact]
    public void UnitsGoPriorityFirstThenNewestFirstEveryLocaleOfAVersionTogether()
    {
        var scope = new PassScope { Priority = [Oldest, PatchVersion.Parse("9.9.1")] };

        var plan = UnitPlanner.Plan([Newest, Middle, Oldest], scope);

        Assert.Equal([Oldest, Newest, Middle], plan.Select(static unit => unit.Version).Distinct());
        Assert.Equal(
            UiLocales.All,
            plan.Take(UiLocales.All.Count).Select(static unit => unit.Locale));
        Assert.Equal(
            plan.Select(static unit => unit.Priority).Order(),
            plan.Select(static unit => unit.Priority));
        Assert.Equal(plan.Count, plan.Select(static unit => unit.Priority).Distinct().Count());
    }

    [Fact]
    public void ScopeKeepsItsRangeAndItsLocale()
    {
        var scope = new PassScope { Oldest = Middle, Newest = Middle, Locale = UiLocale.Ja };

        var plan = UnitPlanner.Plan([Newest, Middle, Oldest], scope);

        Assert.Equal([new PlannedUnit(Middle, UiLocale.Ja, 0)], plan);
    }

    [Fact]
    public void ScopeSurvivesItsStoredForm()
    {
        var scope = new PassScope
        {
            Oldest = Oldest,
            Newest = Newest,
            PageType = ResourceType.Items,
            Locale = UiLocale.ZhHans,
            Priority = [Middle],
        };

        var stored = PassScopeJson.Write(scope);
        var read = PassScopeJson.Read(stored);

        Assert.Equal(
            "{\"oldest\":\"16.17.1\",\"newest\":\"16.19.1\",\"pageType\":\"items\","
                + "\"locale\":\"zh-hans\",\"priority\":[\"16.18.1\"]}",
            stored);
        Assert.Equal(
            (scope.Oldest, scope.Newest, scope.PageType, scope.Locale),
            (read.Oldest, read.Newest, read.PageType, read.Locale));
        Assert.Equal(scope.Priority, read.Priority);
        Assert.Equal("{}", PassScopeJson.Write(PassScope.Everything));
    }

    [Fact]
    public async Task WorkerWritesOneUnitAfterTheOtherInTheirOrder()
    {
        await using var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.AddRange([Newest, Middle, Oldest]);
        var passId = await harness.LaunchAsync(
            new PassScope { Priority = [Middle], Locale = UiLocale.Fr });

        await harness.RunAsync();

        var expected = new[] { Middle, Newest, Oldest }.SelectMany(static version =>
            FakeCatalog.SixPages.Select(path => $"/fr/{version.Value}/{path}"));
        Assert.Equal(expected, harness.Ssr.Pages);
        var pass = await harness.PassAsync(passId);
        Assert.Equal(PrerenderPassState.Completed, pass.State);
        Assert.Equal((18L, 18L), (pass.PagesTotal, pass.PagesWritten));
        Assert.All(
            pass.Units,
            static unit => Assert.Equal(PrerenderUnitState.Completed, unit.State));
    }
}
