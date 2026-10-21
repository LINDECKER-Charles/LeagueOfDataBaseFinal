using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Catalog;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Tests.Admin.Prerender;

/// <summary>
/// The older patches and what an epoch holds of each, locale by locale, with the bases of the
/// launch's estimates: what the launch dialog and the coverage tab read.
/// </summary>
/// <remarks>A host per test, served epoch 2, on a catalog of two older patches.</remarks>
public sealed class AdminPrerenderPatchesTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string Patches = "/api/admin/prerender/patches";
    private const int ServedEpoch = 2;

    private const string Newer = "16.18.1";
    private const string Older = "16.17.1";

    private static readonly IReadOnlyList<string> Codes = [.. UiLocales.All.Select(UiLocales.Code)];

    private readonly FakeCatalog _catalog = new()
    {
        Pinned = { PatchVersion.Parse(Newer), PatchVersion.Parse(Older) },
        Latest = PatchVersion.Parse("16.19.1"),
    };

    private AccountsApp? _app;

    private AccountsApp App => _app ?? throw new InvalidOperationException("Not started.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        _app = await AccountsApp.StartAsync(
            postgres,
            new Dictionary<string, string?>
            {
                ["LoDb:Prerender:ServedEpoch"] =
                    ServedEpoch.ToString(CultureInfo.InvariantCulture),
            },
            services => services.Replace(
                ServiceDescriptor.Singleton<IPrerenderCatalog>(_catalog)));

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task EveryOlderPatchHasACellPerLocaleOfTheServedEpoch()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(Patches);

        var report = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal(
            (ServedEpoch, ServedEpoch),
            (report.GetProperty("epoch").GetInt32(), report.GetProperty("servedEpoch").GetInt32()));
        Assert.Equal(
            UiLocales.All.Select(UiLocales.Code),
            report.GetProperty("locales").EnumerateArray().Select(static code => code.GetString()));
        var rows = report.GetProperty("patches").EnumerateArray().ToList();
        Assert.Equal(
            ["16.18.1", "16.17.1"],
            rows.Select(static row => ApiJson.Text(row, "version")));
        Assert.All(rows, static row => Assert.Equal(
            UiLocales.All.Count,
            ApiJson.Text(row, "cells")!.Length));
        var coverage = report.GetProperty("coverage");
        Assert.Equal(
            (2, 2 * UiLocales.All.Count),
            (coverage.GetProperty("pinnedVersions").GetInt32(),
                coverage.GetProperty("expectedUnits").GetInt32()));
        var estimates = report.GetProperty("estimates");
        Assert.Equal(120, estimates.GetProperty("configuredRendersPerMinute").GetInt32());
        Assert.Equal("EUR", ApiJson.Text(estimates, "currency"));
    }

    [Fact]
    public async Task AnyEpochReadsAndOneBelowOneIsRefused()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        using var visitor = App.Browser();

        using var retired = await admin.GetAsync(Patches + "?epoch=1");
        using var zero = await admin.GetAsync(Patches + "?epoch=0");
        using var refused = await visitor.GetAsync(Patches);
        using var unenrolled = await AdminCalls.OpenUnenrolledAsync(App);
        using var withoutFactor = await unenrolled.GetAsync(Patches);

        var report = await ApiJson.ReadAsync(retired, HttpStatusCode.OK);
        Assert.Equal(1, report.GetProperty("epoch").GetInt32());
        Assert.Equal(["out-of-range"], (await ApiJson.FieldErrorsAsync(zero))["epoch"]);
        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.Unauthorized));
        Assert.Equal(
            "mfa-required",
            await ApiJson.ProblemCodeAsync(withoutFactor, HttpStatusCode.Forbidden));
    }

    // In the served epoch, 16.18.1: English held whole, French failed for good, German held
    // over the items alone by a pass of its own. Epoch 3 holds Spanish only.
    [Fact]
    public async Task EachLocaleHasItsCellAndTheMaskOfTheTypesItHolds()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedAsync(ServedEpoch, null, Unit(Newer, "en", 1_140), Failed(Newer, "fr"));
        await SeedAsync(ServedEpoch, [ResourceType.Items], Unit(Newer, "de", 600));
        await SeedAsync(3, null, Unit(Newer, "es", 1_140));

        var served = await ReadAsync(admin, Patches);
        var standby = await ReadAsync(admin, Patches + "?epoch=3");

        var row = served.GetProperty("patches")[0];
        Assert.Equal(
            ("c", "f", "p", "-"),
            (CellOf(row, "en"), CellOf(row, "fr"), CellOf(row, "de"), CellOf(row, "es")));
        Assert.Equal(("f", "0", "2"), (TypesOf(row, "en"), TypesOf(row, "fr"), TypesOf(row, "de")));
        Assert.Equal(
            (1, 1_740L, 1_140),
            (row.GetProperty("completeLocales").GetInt32(), row.GetProperty("pages").GetInt64(),
                row.GetProperty("pagesPerLocale").GetInt32()));
        Assert.Equal("16.19.1", ApiJson.Text(served, "latestVersion"));
        var coverage = served.GetProperty("coverage");
        Assert.Equal(
            (1, 1, 40),
            (coverage.GetProperty("completeUnits").GetInt32(),
                coverage.GetProperty("failedUnits").GetInt32(),
                coverage.GetProperty("gapUnits").GetInt32()));
        Assert.Equal("c", CellOf(standby.GetProperty("patches")[0], "es"));
    }

    // Any pass over every type, of any epoch, measures the pages of a locale; a pass over one
    // type does not.
    [Fact]
    public async Task PagesOfALocaleComeFromAnyPassOverEveryType()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedAsync(1, null, Unit(Older, "en", 480));
        await SeedAsync(ServedEpoch, [ResourceType.Items], Unit(Newer, "en", 300));

        var rows = (await ReadAsync(admin, Patches)).GetProperty("patches");

        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("pagesPerLocale").ValueKind);
        Assert.Equal(480, rows[1].GetProperty("pagesPerLocale").GetInt32());
    }

    // The shares come from a stored catalog alone: before any completed unit, none is read.
    [Fact]
    public async Task TypeSharesWaitForACompletedUnitAndQueueNothing()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        var before = (await ReadAsync(admin, Patches)).GetProperty("estimates");
        await SeedAsync(ServedEpoch, null, Unit(Older, "en", 6));
        var after = (await ReadAsync(admin, Patches)).GetProperty("estimates");

        Assert.Equal(JsonValueKind.Null, before.GetProperty("typeShares").ValueKind);
        Assert.Equal([PatchVersion.Parse(Older)], _catalog.PageReads);
        var shares = after.GetProperty("typeShares");
        Assert.Equal(
            (0.3333m, 0.3333m, 0.1667m, 0.1667m),
            (shares.GetProperty("champions").GetDecimal(), shares.GetProperty("items").GetDecimal(),
                shares.GetProperty("runes").GetDecimal(),
                shares.GetProperty("summoners").GetDecimal()));
        Assert.Equal(1_000, after.GetProperty("bytesPerPage").GetInt64());
    }

    private static string CellOf(JsonElement row, string locale) =>
        ApiJson.Text(row, "cells")![Codes.ToList().IndexOf(locale)].ToString();

    private static string TypesOf(JsonElement row, string locale) =>
        ApiJson.Text(row, "types")![Codes.ToList().IndexOf(locale)].ToString();

    private static async Task<JsonElement> ReadAsync(BrowserClient browser, string path)
    {
        using var response = await browser.GetAsync(path);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    private async Task SeedAsync(
        int epoch,
        IReadOnlyList<ResourceType>? types,
        params PrerenderUnit[] units)
    {
        var now = App.Clock.GetUtcNow();
        await using var db = App.Database();
        db.PrerenderPasses.Add(new PrerenderPass
        {
            Epoch = epoch,
            State = PrerenderPassState.Completed,
            Scope = PassScopeJson.Write(new PassScope { PageTypes = types }),
            CreatedAt = now,
            FinishedAt = now,
            Units = [.. units],
        });
        await db.SaveChangesAsync(Cancellation);
    }

    // Completed clean, 1,000 bytes a page.
    private PrerenderUnit Unit(string version, string locale, int pages) => new()
    {
        Version = version,
        Locale = locale,
        State = PrerenderUnitState.Completed,
        PagesTotal = pages,
        PagesWritten = pages,
        BytesWritten = pages * 1_000L,
        EndedAt = App.Clock.GetUtcNow(),
    };

    private PrerenderUnit Failed(string version, string locale) => new()
    {
        Version = version,
        Locale = locale,
        State = PrerenderUnitState.Failed,
        EndedAt = App.Clock.GetUtcNow(),
    };
}
