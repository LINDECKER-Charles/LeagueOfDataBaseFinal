using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Testing;

namespace LoDb.Api.Tests.Billing.OwnHost;

/// <summary>One account opens ten checkouts of the API portal an hour at most.</summary>
/// <remarks>A host of its own: the test reaches the limit, which lives in the host.</remarks>
public sealed class PurchaseCheckoutLimitTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string PackPath = "/api/billing/checkout/pack";
    private const string PlanPath = "/api/billing/checkout/plan";
    private const int CheckoutsPerHour = 10;

    private BillingApp? _app;

    private BillingApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task CheckoutsOfOneAccountAreLimitedToTenAnHour()
    {
        var buyer = await App.SeedUserAsync("Pressant");
        await App.SeedKeyAsync(buyer);
        var other = await App.SeedUserAsync("Patient");
        await App.SeedKeyAsync(other);
        using var browser = await SignedInAsync(buyer);
        using var otherBrowser = await SignedInAsync(other);
        var offer = new { pack = "small", plan = "annual" };
        var statuses = new List<HttpStatusCode>();

        // Packs and plans share the limit.
        for (var attempt = 0; attempt < CheckoutsPerHour; attempt++)
        {
            var path = attempt % 2 == 0 ? PackPath : PlanPath;
            using var opened = await browser.PostAsync(path, offer);
            statuses.Add(opened.StatusCode);
        }

        using var refused = await browser.PostAsync(PackPath, new { pack = "small" });
        using var otherAccount = await otherBrowser.PostAsync(PackPath, new { pack = "small" });

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(
            "rate-limited",
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.OK, otherAccount.StatusCode);
        Assert.Equal(CheckoutsPerHour + 1, App.Gateway.Sessions.Count);
    }

    public async ValueTask InitializeAsync() => _app = await BillingApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private async Task<BrowserClient> SignedInAsync(User account)
    {
        var browser = App.Browser();
        using var response = await browser.SignInAsync(account.UserName!);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return browser;
    }
}
