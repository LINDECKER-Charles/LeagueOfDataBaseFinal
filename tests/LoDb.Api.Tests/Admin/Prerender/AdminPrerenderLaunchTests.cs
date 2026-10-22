using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Tests.Admin.Prerender;

/// <summary>
/// The launch of a pass as the desk plans it: what the served epoch holds already is left out
/// when asked, counted and journaled; a pass with nothing to render is refused before any row
/// is written; a pass active refuses the launch before the planning reads anything.
/// </summary>
/// <remarks>A host per test, served epoch 2, on a catalog of two older patches.</remarks>
public sealed class AdminPrerenderLaunchTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private const string Passes = "/api/admin/prerender/passes";
    private const int ServedEpoch = 2;
    private const string Section = "LoDb:Prerender:";

    private static readonly PatchVersion Newer = PatchVersion.Parse("16.18.1");
    private static readonly PatchVersion Older = PatchVersion.Parse("16.17.1");
    private static readonly string[] French = ["fr"];

    private readonly FakeCatalog _catalog = new() { Pinned = { Newer, Older } };
    private AccountsApp? _app;

    private AccountsApp App => _app ?? throw new InvalidOperationException("Not started.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        _app = await AccountsApp.StartAsync(postgres, Settings, services =>
            services.Replace(ServiceDescriptor.Singleton<IPrerenderCatalog>(_catalog)));

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnitsCompleteAlreadyAreLeftOutCountedAndJournaled()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedCompletedAsync(Newer);

        using var response = await admin.PostAsync(
            Passes,
            new { locales = French, skipComplete = true });

        var started = await ApiJson.ReadAsync(response, HttpStatusCode.Created);
        Assert.Equal(
            (1, 1),
            (started.GetProperty("units").GetInt32(),
                started.GetProperty("skippedUnits").GetInt32()));
        var entry = (await App.AuditAsync())[^1];
        Assert.Equal(AuditAction.AdminPrerenderStart, entry.Action);
        using var meta = JsonDocument.Parse(entry.Meta!);
        Assert.Equal(
            (1, 1),
            (meta.RootElement.GetProperty("units").GetInt32(),
                meta.RootElement.GetProperty("skipped").GetInt32()));
    }

    [Fact]
    public async Task PassWhoseUnitsAreAllCompleteIsRefused()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedCompletedAsync(Newer, Older);

        using var response = await admin.PostAsync(
            Passes,
            new { locales = French, skipComplete = true });

        Assert.Equal(["nothing-to-render"], (await ApiJson.FieldErrorsAsync(response))["scope"]);
        Assert.Equal(1, await PassCountAsync());
    }

    [Fact]
    public async Task NoOlderPatchAtAllIsRefusedAsNonePinned()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        _catalog.Pinned.Clear();

        using var response = await admin.PostAsync(Passes, new { });

        Assert.Equal(["none-pinned"], (await ApiJson.FieldErrorsAsync(response))["versions"]);
        Assert.Equal(0, await PassCountAsync());
    }

    [Fact]
    public async Task OpenersPassIsStoredExpectingItsPagesAndShownAsSuch()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        _catalog.Openers[Newer] = new HashSet<string>(["items/1036-long-sword"]);

        using var response = await admin.PostAsync(
            Passes,
            new { versions = new[] { Newer.Value }, locales = French, pages = "openers" });

        await ApiJson.ReadAsync(response, HttpStatusCode.Created);
        await using (var db = App.Database())
        {
            var pass = await db.PrerenderPasses.Include(static pass => pass.Units)
                .SingleAsync(Cancellation);
            Assert.Equal(PassPages.Openers, PassScopeJson.Read(pass.Scope).Pages);
            Assert.Equal(2, Assert.Single(pass.Units).PagesExpected);
        }

        using var report = await admin.GetAsync("/api/admin/prerender");
        var passes = (await ApiJson.ReadAsync(report, HttpStatusCode.OK)).GetProperty("passes");
        Assert.Equal("openers", passes[0].GetProperty("scope").GetProperty("pages").GetString());
    }

    [Fact]
    public async Task UnknownSetOfPagesIsRefusedOnItsField()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.PostAsync(Passes, new { pages = "everything" });

        Assert.Equal(["malformed"], (await ApiJson.FieldErrorsAsync(response))["pages"]);
        Assert.Equal(0, await PassCountAsync());
    }

    [Fact]
    public async Task ActivePassRefusesTheLaunchBeforeThePlanningReadsAnything()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await using (var db = App.Database())
        {
            db.PrerenderPasses.Add(new PrerenderPass
            {
                Epoch = ServedEpoch,
                State = PrerenderPassState.Running,
                RunningSince = App.Clock.GetUtcNow(),
            });
            await db.SaveChangesAsync(Cancellation);
        }

        var reads = _catalog.PinnedReads;
        using var response = await admin.PostAsync(Passes, new { epoch = 3 });

        Assert.Equal(
            "prerender-pass-active",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Conflict));
        Assert.Equal(reads, _catalog.PinnedReads);
    }

    private static Dictionary<string, string?> Settings => new()
    {
        [Section + "PublicHost"] = PrerenderHarness.PublicHost,
        [Section + "SsrBaseUrl"] = PrerenderHarness.SsrBaseUrl,
        [Section + "ServedEpoch"] = ServedEpoch.ToString(CultureInfo.InvariantCulture),
        [Section + "Endpoint"] = "https://s3.fr-par.scw.cloud",
        [Section + "Bucket"] = "lodb-pages-test",
        [Section + "AccessKey"] = "writer",
        [Section + "SecretKey"] = "writer-secret",
    };

    // A completed pass over every type of the served epoch, clean in French for each version.
    private async Task SeedCompletedAsync(params PatchVersion[] versions)
    {
        var now = App.Clock.GetUtcNow();
        await using var db = App.Database();
        db.PrerenderPasses.Add(new PrerenderPass
        {
            Epoch = ServedEpoch,
            State = PrerenderPassState.Completed,
            CreatedAt = now,
            FinishedAt = now,
            Units =
            [
                .. versions.Select(version => new PrerenderUnit
                {
                    Version = version.Value,
                    Locale = "fr",
                    State = PrerenderUnitState.Completed,
                    PagesTotal = 6,
                    PagesWritten = 6,
                    EndedAt = now,
                }),
            ],
        });
        await db.SaveChangesAsync(Cancellation);
    }

    private async Task<int> PassCountAsync()
    {
        await using var db = App.Database();
        return await db.PrerenderPasses.CountAsync(Cancellation);
    }
}
