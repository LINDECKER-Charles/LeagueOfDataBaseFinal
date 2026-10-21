using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin.Prerender;

/// <summary>
/// The units of one version of a pass, a row per locale in the site's order: what the monitor
/// shows when a version of its matrix is opened.
/// </summary>
public sealed class AdminPrerenderPassVersionTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string Passes = "/api/admin/prerender/passes";
    private const string Version = "16.18.1";

    private AccountsApp? _app;

    private AccountsApp App => _app ?? throw new InvalidOperationException("Not started.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task VersionShowsItsUnitsInTheOrderOfTheLocales()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var id = await SeedAsync();

        var units = await ReadAsync(admin, $"{Passes}/{id}/versions/{Version}");
        var none = await ReadAsync(admin, $"{Passes}/{id}/versions/16.1.1");

        Assert.Equal((id, Version), (units.GetProperty("passId").GetInt64(),
            ApiJson.Text(units, "version")));
        Assert.Equal(
            UiLocales.All.Select(UiLocales.Code),
            units.GetProperty("units").EnumerateArray()
                .Select(static unit => ApiJson.Text(unit, "locale")));
        Assert.Empty(none.GetProperty("units").EnumerateArray());
    }

    [Fact]
    public async Task MalformedVersionIsRefusedAndAnUnknownPassIsNotFound()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var id = await SeedAsync();

        using var malformed = await admin.GetAsync($"{Passes}/{id}/versions/latest");
        using var unknown = await admin.GetAsync($"{Passes}/{id + 1}/versions/{Version}");

        Assert.Equal(["malformed"], (await ApiJson.FieldErrorsAsync(malformed))["version"]);
        Assert.Equal(
            "prerender-pass-not-found",
            await ApiJson.ProblemCodeAsync(unknown, HttpStatusCode.NotFound));
    }

    private static async Task<JsonElement> ReadAsync(BrowserClient browser, string path)
    {
        using var response = await browser.GetAsync(path);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    // The version's units written in the reverse order of the locales.
    private async Task<long> SeedAsync()
    {
        var now = App.Clock.GetUtcNow();
        var pass = new PrerenderPass
        {
            Epoch = 1,
            State = PrerenderPassState.Completed,
            CreatedAt = now.AddHours(-1),
            FinishedAt = now,
            Units =
            [
                .. UiLocales.All.Reverse().Select(locale => new PrerenderUnit
                {
                    Version = Version,
                    Locale = UiLocales.Code(locale),
                    State = PrerenderUnitState.Completed,
                    UpdatedAt = now,
                }),
            ],
        };
        await using var db = App.Database();
        db.PrerenderPasses.Add(pass);
        await db.SaveChangesAsync(Cancellation);
        return pass.Id;
    }
}
