using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Billing.OwnHost;

/// <summary>One address opens ten donation checkouts an hour at most.</summary>
/// <remarks>A host of its own: the test reaches the limit, which lives in the host.</remarks>
public sealed class DonationCheckoutLimitTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string CheckoutPath = "/api/donations/checkout";
    private const int CheckoutsPerHour = 10;

    private BillingApp? _app;

    private BillingApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task OneAddressOpensTenCheckoutsAnHour()
    {
        using var browser = App.Browser();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt <= CheckoutsPerHour; attempt++)
        {
            using var response = await OpenFromAsync(browser, "203.0.113.7");
            statuses.Add(response.StatusCode);
        }

        using var other = await OpenFromAsync(browser, "203.0.113.8");

        Assert.Equal(
            [.. Enumerable.Repeat(HttpStatusCode.OK, CheckoutsPerHour),
                HttpStatusCode.TooManyRequests],
            statuses);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    public async ValueTask InitializeAsync() => _app = await BillingApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static async Task<HttpResponseMessage> OpenFromAsync(
        BrowserClient browser,
        string address)
    {
        using var request = browser.Post(CheckoutPath, new { amountCents = 500 });
        request.Headers.Add(BrowserClient.ForwardedForHeader, address);
        return await browser.SendAsync(request);
    }
}
