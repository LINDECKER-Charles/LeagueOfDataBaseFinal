using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Passes.Planning;
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
    public void ScopeKeepsItsListedVersionsAndLocales()
    {
        var scope = new PassScope { Versions = [Oldest, Newest], Locales = [UiLocale.Ja] };

        var plan = UnitPlanner.Plan([Newest, Middle, Oldest], scope);

        Assert.Equal(
            [new PlannedUnit(Newest, UiLocale.Ja, 0), new PlannedUnit(Oldest, UiLocale.Ja, 1)],
            plan);
    }

    [Fact]
    public void ScopeIsStoredInItsListedForm()
    {
        var scope = new PassScope
        {
            Versions = [Middle, Oldest],
            PageTypes = [ResourceType.Items, ResourceType.Runes],
            Locales = [UiLocale.En, UiLocale.ZhHans],
            Priority = [Middle],
            SkipComplete = true,
        };

        var stored = PassScopeJson.Write(scope);
        var read = PassScopeJson.Read(stored);

        Assert.Equal(
            "{\"versions\":[\"16.18.1\",\"16.17.1\"],\"locales\":[\"en\",\"zh-hans\"],"
                + "\"pageTypes\":[\"items\",\"runes\"],\"priority\":[\"16.18.1\"],"
                + "\"skipComplete\":true}",
            stored);
        Assert.Equal(stored, PassScopeJson.Write(read));
        Assert.Equal("{}", PassScopeJson.Write(PassScope.Everything));
        Assert.Equal("{\"versions\":[\"16.19.1\"]}", PassScopeJson.Write(PassScope.Of(Newest)));
    }

    // A pass launched before the lists keeps reading: its range, its one type, its one locale.
    [Fact]
    public void ScopeStoredBeforeTheListsStillReads()
    {
        var read = PassScopeJson.Read(
            "{\"oldest\":\"16.17.1\",\"newest\":\"16.18.1\",\"pageType\":\"items\","
                + "\"locale\":\"fr\",\"priority\":[\"16.18.1\"]}");

        Assert.Equal((Oldest, Middle), (read.Oldest, read.Newest));
        Assert.Equal([ResourceType.Items], read.PageTypes);
        Assert.Equal([UiLocale.Fr], read.Locales);
        Assert.Equal([Middle], read.Priority);
        Assert.Equal([Middle, Oldest], new[] { Newest, Middle, Oldest }.Where(read.Admits));
        Assert.False(read.IsWhole);
        Assert.Equal(
            "{\"oldest\":\"16.17.1\",\"newest\":\"16.18.1\",\"locales\":[\"fr\"],"
                + "\"pageTypes\":[\"items\"],\"priority\":[\"16.18.1\"]}",
            PassScopeJson.Write(read));
    }

    [Fact]
    public void FullListsOfLocalesAndTypesReadAsEveryOne()
    {
        var read = PassScopeJson.Read(
            "{\"locales\":[" + string.Join(',', UiLocales.All.Reverse().Select(Quoted)) + "],"
                + "\"pageTypes\":[\"summoners\",\"runes\",\"items\",\"champions\"]}");

        Assert.Null(read.Locales);
        Assert.Null(read.PageTypes);
        Assert.True(read.IsWhole);
        Assert.Throws<FormatException>(() => PassScopeJson.Read("{\"locales\":[\"xx\"]}"));
    }

    [Fact]
    public async Task WorkerWritesOneUnitAfterTheOtherInTheirOrder()
    {
        await using var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.AddRange([Newest, Middle, Oldest]);
        var passId = await harness.LaunchAsync(
            new PassScope { Priority = [Middle], Locales = [UiLocale.Fr] });

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

    // Epoch 1 holds 16.18.1 in French over the items alone, 16.19.1 in English over every type.
    [Fact]
    public async Task SkippingWhatIsCompleteHeedsTheTypesAndKeepsTheRanksContiguous()
    {
        await using var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.AddRange([Newest, Middle]);
        await SeedAsync(harness, [ResourceType.Items], Unit(Middle, "fr", pages: 300));
        await SeedAsync(harness, null, Unit(Newest, "en", pages: 1_140));
        var bilingual = new PassScope { Locales = [UiLocale.En, UiLocale.Fr], SkipComplete = true };

        var items = await PlanAsync(harness, bilingual with { PageTypes = [ResourceType.Items] });
        var every = await PlanAsync(harness, bilingual);

        // 16.18.1 measured over the items alone, by the French unit.
        Assert.Equal(
            [
                new PlannedUnit(Newest, UiLocale.Fr, 0),
                new PlannedUnit(Middle, UiLocale.En, 1) { PagesExpected = 300 },
            ],
            items.Units);
        Assert.Equal(2, items.Skipped);
        Assert.Equal(
            [(Newest, UiLocale.Fr, 0), (Middle, UiLocale.En, 1), (Middle, UiLocale.Fr, 2)],
            every.Units.Select(static unit => (unit.Version, unit.Locale, unit.Priority)));
        Assert.Equal(1, every.Skipped);
    }

    // The pages a unit should list come from a former unit over the same types, any epoch.
    [Fact]
    public async Task ExpectedPagesComeFromAFormerPassOverTheSameTypes()
    {
        await using var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.AddRange([Newest, Middle]);
        await SeedAsync(harness, [ResourceType.Items], Unit(Newest, "fr", pages: 300));
        await SeedAsync(harness, null, Unit(Middle, "fr", pages: 1_140), epoch: 2);

        var plan = await PlanAsync(harness, new PassScope { Locales = [UiLocale.En] });
        var passId = await harness.LaunchAsync(new PassScope { Locales = [UiLocale.En] });

        Assert.Equal([null, 1_140], plan.Units.Select(static unit => unit.PagesExpected));
        Assert.Equal(
            [null, 1_140],
            (await harness.PassAsync(passId)).Units
                .OrderBy(static unit => unit.Priority)
                .Select(static unit => unit.PagesExpected));
    }

    private static string Quoted(UiLocale locale) => $"\"{UiLocales.Code(locale)}\"";

    private static Task<PassPlan> PlanAsync(PrerenderHarness harness, PassScope scope) =>
        harness.Get<PassPlanning>().PlanAsync(
            new PassLaunch { Epoch = 1, Scope = scope },
            TestContext.Current.CancellationToken);

    // A completed pass over the types given, with one unit.
    private static async Task SeedAsync(
        PrerenderHarness harness,
        IReadOnlyList<ResourceType>? types,
        PrerenderUnit unit,
        int epoch = 1)
    {
        var now = harness.Clock.GetUtcNow();
        await using var db = harness.Database.CreateContext();
        db.PrerenderPasses.Add(new PrerenderPass
        {
            Epoch = epoch,
            State = PrerenderPassState.Completed,
            Scope = PassScopeJson.Write(new PassScope { PageTypes = types }),
            CreatedAt = now,
            FinishedAt = now,
            Units = [unit],
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static PrerenderUnit Unit(PatchVersion version, string locale, int pages) => new()
    {
        Version = version.Value,
        Locale = locale,
        State = PrerenderUnitState.Completed,
        PagesTotal = pages,
        PagesWritten = pages,
        Cursor = pages,
    };
}
