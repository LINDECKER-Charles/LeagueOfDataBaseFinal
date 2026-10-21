using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// The pre-rendering's admin API: an administrator with a second factor alone launches,
/// pauses, resumes and cancels a pass, one active at a time, each action in the journal; the
/// report shows the passes, the served epoch of the settings and what the store holds.
/// </summary>
/// <remarks>
/// A host per test, configured for a store it never reaches (the worker is off), on a catalog
/// of two older patches; served epoch 2.
/// </remarks>
public sealed class AdminPrerenderTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private const string Report = "/api/admin/prerender";
    private const string Passes = Report + "/passes";
    private const int ServedEpoch = 2;
    private const string Section = "LoDb:Prerender:";

    private static readonly PatchVersion Newer = PatchVersion.Parse("16.18.1");
    private static readonly PatchVersion Older = PatchVersion.Parse("16.17.1");

    private static readonly string[] French = ["fr"];
    private static readonly string[] RepeatedTypes = ["runes", "items", "runes"];
    private static readonly string[] NotPinned = ["15.1.1"];
    private static readonly string[] OneMalformedVersion = ["16.18.1", "latest"];
    private static readonly string[] OneUnknownType = ["items", "skins"];

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
    public async Task OnlyAnAdministratorWithSecondFactorReachesTheApi()
    {
        using var visitor = App.Browser();
        using var member = await AdminBrowser.OpenMemberAsync(App);
        using var unenrolled = await AdminCalls.OpenUnenrolledAsync(App);

        foreach (var (browser, status, code) in new[]
        {
            (visitor, HttpStatusCode.Unauthorized, "authentication-required"),
            (member, HttpStatusCode.Forbidden, "forbidden"),
            (unenrolled, HttpStatusCode.Forbidden, "mfa-required"),
        })
        {
            using var read = await browser.GetAsync(Report);
            using var start = await browser.PostAsync(Passes, new { });
            Assert.Equal(code, await ApiJson.ProblemCodeAsync(read, status));
            Assert.Equal(code, await ApiJson.ProblemCodeAsync(start, status));
        }

        Assert.Empty(await PassesAsync());
    }

    [Fact]
    public async Task StartOpensAPassOnTheServedEpochAndIsAudited()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.PostAsync(Passes, new { locales = French });

        var started = await ApiJson.ReadAsync(response, HttpStatusCode.Created);
        Assert.Equal(ServedEpoch, started.GetProperty("epoch").GetInt32());
        Assert.Equal(2, started.GetProperty("units").GetInt32());
        Assert.Equal(0, started.GetProperty("skippedUnits").GetInt32());
        Assert.Empty(started.GetProperty("ignoredVersions").EnumerateArray());
        var id = started.GetProperty("id").GetInt64();
        var entry = await LastAuditAsync(AuditAction.AdminPrerenderStart);
        Assert.Equal(
            (AuditTargetType.PrerenderPass, Id(id), "epoch 2"),
            (entry.TargetType, entry.TargetId, entry.Target));
        Assert.Equal(AdminBrowser.AdminName, entry.Actor);
        Assert.Equal("{\"locales\":[\"fr\"],\"locale\":\"fr\"}", Meta(entry, "scope"));
        using (var meta = JsonDocument.Parse(entry.Meta!))
        {
            Assert.Equal(2, meta.RootElement.GetProperty("units").GetInt32());
        }

        var current = (await ReadAsync(admin)).GetProperty("current");
        Assert.Equal(
            ("running", AdminBrowser.AdminName),
            (State(current), Text(current, "authorName")));
        Assert.Equal(2, current.GetProperty("units").GetProperty("pending").GetInt32());
    }

    [Fact]
    public async Task ListsAreReadOnceEachAndVersionsNotPinnedAreNamedBack()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.PostAsync(Passes, new
        {
            versions = new[] { "15.1.1", Older.Value, "15.1.1" },
            locales = UiLocales.All.Reverse().Select(UiLocales.Code),
            pageTypes = RepeatedTypes,
            skipComplete = true,
        });

        var started = await ApiJson.ReadAsync(response, HttpStatusCode.Created);
        Assert.Equal(UiLocales.All.Count, started.GetProperty("units").GetInt32());
        Assert.Equal(
            ["15.1.1"],
            started.GetProperty("ignoredVersions").EnumerateArray()
                .Select(static version => version.GetString()));
        var pass = Assert.Single(await PassesAsync());
        Assert.Equal(
            "{\"versions\":[\"15.1.1\",\"16.17.1\"],\"oldest\":\"15.1.1\","
                + "\"newest\":\"16.17.1\",\"pageTypes\":[\"items\",\"runes\"],"
                + "\"skipComplete\":true}",
            PassScopeJson.Write(PassScopeJson.Read(pass.Scope)));
        var scope = (await ReadAsync(admin)).GetProperty("current").GetProperty("scope");
        Assert.Equal(
            (2, "16.17.1", "15.1.1"),
            (scope.GetProperty("versionCount").GetInt32(), Text(scope, "newestListed"),
                Text(scope, "oldestListed")));
    }

    [Fact]
    public async Task VersionsOfNoOlderPatchAreRefusedBeforeAnythingOpens()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.PostAsync(Passes, new { versions = NotPinned });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["none-pinned"], errors["versions"]);
        Assert.Empty(await PassesAsync());
    }

    [Fact]
    public async Task SecondStartIsRefusedWhileAPassIsActive()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        using var first = await admin.PostAsync(Passes, new { });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await admin.PostAsync(Passes, new { epoch = 3 });

        Assert.Equal(
            "prerender-pass-active",
            await ApiJson.ProblemCodeAsync(second, HttpStatusCode.Conflict));
        Assert.Single(await PassesAsync());
    }

    [Fact]
    public async Task EachMoveIsAuditedAndAMoveThatDoesNotApplyIsRefused()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        using var start = await admin.PostAsync(Passes, new { epoch = 3 });
        var id = (await ApiJson.ReadAsync(start, HttpStatusCode.Created)).GetProperty("id");
        var pass = $"{Passes}/{id.GetInt64()}";

        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(admin, pass + "/pause"));
        Assert.Equal("prerender-pass-state", await ProblemAsync(admin, pass + "/pause"));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(admin, pass + "/resume"));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(admin, pass + "/cancel"));
        Assert.Equal("prerender-pass-state", await ProblemAsync(admin, pass + "/resume"));
        using var unknown = await admin.PostAsync($"{Passes}/{id.GetInt64() + 1}/cancel");

        Assert.Equal(
            "prerender-pass-not-found",
            await ApiJson.ProblemCodeAsync(unknown, HttpStatusCode.NotFound));
        Assert.Equal(
            [AuditAction.AdminPrerenderStart, AuditAction.AdminPrerenderPause,
                AuditAction.AdminPrerenderResume, AuditAction.AdminPrerenderCancel],
            (await App.AuditOfAsync("epoch 3")).Select(static entry => entry.Action));
        var cancelled = Assert.Single(await PassesAsync());
        Assert.Equal(PrerenderPassState.Cancelled, cancelled.State);
    }

    [Fact]
    public async Task InvalidRequestNamesEachFieldAndOpensNothing()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.PostAsync(Passes, new
        {
            epoch = 1,
            versions = OneMalformedVersion,
            locales = Array.Empty<string>(),
            pageTypes = OneUnknownType,
            priority = Enumerable.Repeat("16.18.1", 101),
        });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["retired"], errors["epoch"]);
        Assert.Equal(["malformed"], errors["versions"]);
        Assert.Equal(["required"], errors["locales"]);
        Assert.Equal(["malformed"], errors["pageTypes"]);
        Assert.Equal(["too-many"], errors["priority"]);
        Assert.Empty(await PassesAsync());
    }

    [Fact]
    public async Task ListsBeyondTheirBoundAreRefused()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.PostAsync(Passes, new
        {
            epoch = 0,
            versions = Enumerable.Repeat("16.18.1", 1001),
            pageTypes = Array.Empty<string>(),
            priority = OneMalformedVersion,
        });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["out-of-range"], errors["epoch"]);
        Assert.Equal(["too-many"], errors["versions"]);
        Assert.Equal(["required"], errors["pageTypes"]);
        Assert.Equal(["malformed"], errors["priority"]);
        Assert.False(errors.ContainsKey("locales"));
    }

    [Fact]
    public async Task UnconfiguredHostReportsItAndRefusesToStart()
    {
        await using var dormant = await AccountsApp.StartAsync(postgres);
        using var admin = await AdminBrowser.OpenAsync(dormant);

        using var start = await admin.PostAsync(Passes, new { });
        var report = await ReadAsync(admin);

        Assert.Equal(
            "prerender-dormant",
            await ApiJson.ProblemCodeAsync(start, HttpStatusCode.Conflict));
        Assert.False(report.GetProperty("isConfigured").GetBoolean());
        Assert.Equal(1, report.GetProperty("servedEpoch").GetInt32());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("current").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("storeBucket").ValueKind);
    }

    [Fact]
    public async Task ReportNamesTheBucketOfTheStore()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        var report = await ReadAsync(admin);

        Assert.Equal("lodb-pages-test", Text(report, "storeBucket"));
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

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static string? State(JsonElement pass) => Text(pass, "state");

    private static string? Text(JsonElement element, string name) => ApiJson.Text(element, name);

    private static string? Meta(AuditLogEntry entry, string key) => AccountsApp.Meta(entry, key);

    private static async Task<HttpStatusCode> StatusAsync(BrowserClient browser, string path)
    {
        using var response = await browser.PostAsync(path);
        return response.StatusCode;
    }

    private static async Task<string?> ProblemAsync(BrowserClient browser, string path)
    {
        using var response = await browser.PostAsync(path);
        return await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Conflict);
    }

    private static async Task<JsonElement> ReadAsync(BrowserClient browser)
    {
        using var response = await browser.GetAsync(Report);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    private async Task<AuditLogEntry> LastAuditAsync(AuditAction action)
    {
        var entry = (await App.AuditAsync())[^1];
        Assert.Equal(action, entry.Action);
        return entry;
    }

    private async Task<List<PrerenderPass>> PassesAsync()
    {
        await using var db = App.Database();
        return await db.PrerenderPasses.AsNoTracking()
            .OrderBy(static pass => pass.Id)
            .ToListAsync(Cancellation);
    }
}
