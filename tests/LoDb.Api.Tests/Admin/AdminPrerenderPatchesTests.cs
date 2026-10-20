using System.Globalization;
using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Tests.Admin;

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

    private readonly FakeCatalog _catalog = new()
    {
        Pinned = { PatchVersion.Parse("16.18.1"), PatchVersion.Parse("16.17.1") },
    };

    private AccountsApp? _app;

    private AccountsApp App => _app ?? throw new InvalidOperationException("Not started.");

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

        var report = await ApiJson.ReadAsync(retired, HttpStatusCode.OK);
        Assert.Equal(1, report.GetProperty("epoch").GetInt32());
        Assert.Equal(["out-of-range"], (await ApiJson.FieldErrorsAsync(zero))["epoch"]);
        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.Unauthorized));
    }
}
