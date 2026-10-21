using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// The epoch ready to serve, judged by its coverage whatever the passes that filled it, the
/// coverage of each epoch not retired in the report, and what is left of the active pass.
/// </summary>
/// <remarks>
/// A host per test, served epoch 2, on a catalog of two older patches; the passes written
/// straight into the database.
/// </remarks>
public sealed class AdminPrerenderReadinessTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string Report = "/api/admin/prerender";
    private const string Newer = "16.18.1";
    private const string Older = "16.17.1";

    private readonly FakeCatalog _catalog = new()
    {
        Pinned = { PatchVersion.Parse(Newer), PatchVersion.Parse(Older) },
    };

    private AccountsApp? _app;

    private AccountsApp App => _app ?? throw new InvalidOperationException("Not started.");

    private DateTimeOffset Now => App.Clock.GetUtcNow();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        _app = await AccountsApp.StartAsync(
            postgres,
            new Dictionary<string, string?> { ["LoDb:Prerender:ServedEpoch"] = "2" },
            services => services.Replace(
                ServiceDescriptor.Singleton<IPrerenderCatalog>(_catalog)));

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    // Two passes of one version each; the unit Data Dragon did not know failed for good, and
    // another completed with two failed pages: the SSR serves both, the epoch is ready.
    [Fact]
    public async Task EpochFilledByTargetedPassesIsReadyDespiteUnitsFailedForGood()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedAsync(3, Newer, PrerenderPassState.Completed, EveryLocale(Newer));
        var older = EveryLocale(Older);
        (older[0].State, older[0].PagesWritten) = (PrerenderUnitState.Failed, 0);
        older[1].PagesFailed = 2;
        await SeedAsync(3, Older, PrerenderPassState.Completed, older);

        var report = await ReadAsync(admin);

        Assert.Equal(3, report.GetProperty("readyEpoch").GetInt32());
        var coverage = CoverageOf(report, 3);
        Assert.Equal(
            (41, 40, 1, 0, 2L),
            (coverage.GetProperty("completeUnits").GetInt32(),
                coverage.GetProperty("cleanUnits").GetInt32(),
                coverage.GetProperty("failedUnits").GetInt32(),
                coverage.GetProperty("gapUnits").GetInt32(),
                coverage.GetProperty("failedPages").GetInt64()));
    }

    // One locale of the older patch holds the items alone: a gap, until a pass over every type
    // renders it.
    [Fact]
    public async Task EpochWithAPartialOrAnAbsentLocaleIsNotReady()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedAsync(3, Newer, PrerenderPassState.Completed, EveryLocale(Newer));
        await SeedAsync(3, Older, PrerenderPassState.Completed, [.. EveryLocale(Older).Skip(2)]);
        await SeedAsync(
            3,
            Older,
            PrerenderPassState.Completed,
            [.. EveryLocale(Older).Take(1)],
            [ResourceType.Items]);

        var report = await ReadAsync(admin);

        Assert.Equal(JsonValueKind.Null, report.GetProperty("readyEpoch").ValueKind);
        Assert.Equal(2, CoverageOf(report, 3).GetProperty("gapUnits").GetInt32());
    }

    // Epochs 3 and 4 hold the leftovers of cancelled passes, epoch 1 is retired.
    [Fact]
    public async Task EveryEpochNotRetiredHasItsCoverage()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedAsync(1, Newer, PrerenderPassState.Completed, EveryLocale(Newer));
        await SeedAsync(3, Newer, PrerenderPassState.Cancelled, [.. EveryLocale(Newer).Take(3)]);
        await SeedAsync(4, Newer, PrerenderPassState.Cancelled, []);

        var report = await ReadAsync(admin);

        var epochs = report.GetProperty("storage").GetProperty("epochs").EnumerateArray()
            .ToDictionary(static epoch => epoch.GetProperty("epoch").GetInt32());
        Assert.Equal([4, 3, 2, 1], epochs.Keys);
        Assert.Equal("standby", ApiJson.Text(epochs[4], "state"));
        Assert.Equal(3, epochs[3].GetProperty("coverage").GetProperty("completeUnits").GetInt32());
        Assert.Equal(42, epochs[2].GetProperty("coverage").GetProperty("gapUnits").GetInt32());
        Assert.Equal(JsonValueKind.Null, epochs[1].GetProperty("coverage").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("readyEpoch").ValueKind);
    }

    // The newest patch listed 1,140 pages a locale; the older one's units, not reached yet,
    // expect 500 each, as a former pass measured: not the 1,140 of the listed units' mean.
    [Fact]
    public async Task PagesLeftCountEachUnitToComeAtItsVersionsPages()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var done = EveryLocale(Newer)[0];
        (done.PagesTotal, done.PagesWritten) = (1_140, 1_140);
        var current = EveryLocale(Newer)[1];
        (current.State, current.PagesTotal, current.PagesWritten, current.EndedAt) =
            (PrerenderUnitState.Running, 1_140, 140, null);
        List<PrerenderUnit> next = [.. EveryLocale(Older).Take(2)];
        next.ForEach(static unit =>
            (unit.State, unit.PagesTotal, unit.PagesWritten, unit.EndedAt, unit.PagesExpected) =
                (PrerenderUnitState.Pending, 0, 0, null, 500));
        await using (var db = App.Database())
        {
            db.PrerenderPasses.Add(new PrerenderPass
            {
                Epoch = 2,
                State = PrerenderPassState.Running,
                CreatedAt = Now,
                RunningSince = Now,
                PagesTotal = 2_280,
                PagesWritten = 1_280,
                Units = [done, current, .. next],
            });
            await db.SaveChangesAsync(Cancellation);
        }

        var report = await ReadAsync(admin);

        Assert.Equal(2_000, report.GetProperty("current").GetProperty("pagesLeft").GetInt64());
    }

    private static JsonElement CoverageOf(JsonElement report, int epoch) =>
        report.GetProperty("storage").GetProperty("epochs").EnumerateArray()
            .Single(row => row.GetProperty("epoch").GetInt32() == epoch)
            .GetProperty("coverage");

    private static async Task<JsonElement> ReadAsync(BrowserClient browser)
    {
        using var response = await browser.GetAsync(Report);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    private async Task SeedAsync(
        int epoch,
        string version,
        PrerenderPassState state,
        List<PrerenderUnit> units,
        IReadOnlyList<ResourceType>? types = null)
    {
        await using var db = App.Database();
        db.PrerenderPasses.Add(new PrerenderPass
        {
            Epoch = epoch,
            State = state,
            Scope = PassScopeJson.Write(new PassScope
            {
                Versions = [PatchVersion.Parse(version)],
                PageTypes = types,
            }),
            CreatedAt = Now,
            FinishedAt = Now,
            Units = units,
        });
        await db.SaveChangesAsync(Cancellation);
    }

    // A clean unit of the version in each of the 21 locales, in the site's order.
    private List<PrerenderUnit> EveryLocale(string version) =>
    [
        .. UiLocales.All.Select(locale => new PrerenderUnit
        {
            Version = version,
            Locale = UiLocales.Code(locale),
            State = PrerenderUnitState.Completed,
            PagesTotal = 500,
            PagesWritten = 500,
            BytesWritten = 500_000,
            EndedAt = Now,
        }),
    ];
}
