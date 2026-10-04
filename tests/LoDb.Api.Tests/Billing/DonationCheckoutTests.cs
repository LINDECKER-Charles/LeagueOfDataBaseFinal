using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// The donation page's calls: its options, then the checkout it opens for an amount between
/// 1 and 500 euros, naming a signed-in donor, behind the forgery guard and ten an hour.
/// </summary>
public sealed class DonationCheckoutTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string OptionsPath = "/api/donations/options";
    private const string CheckoutPath = "/api/donations/checkout";
    private const string ForwardedFor = "X-Forwarded-For";
    private const int CheckoutsPerHour = 10;

    private BillingApp? _app;

    private BillingApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task OptionsDescribeTheForm()
    {
        using var browser = App.Browser();

        using var response = await browser.GetAsync(OptionsPath);

        var options = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.True(options.GetProperty("available").GetBoolean());
        Assert.Equal(
            [300L, 500L, 1_000L, 2_500L],
            options.GetProperty("presets").EnumerateArray().Select(static p => p.GetInt64()));
        Assert.Equal(100, options.GetProperty("minCents").GetInt64());
        Assert.Equal(50_000, options.GetProperty("maxCents").GetInt64());
        Assert.Equal("eur", ApiJson.Text(options, "currency"));
    }

    [Fact]
    public async Task VisitorIsSentToStripe()
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(
            CheckoutPath,
            new { amountCents = 1_500, locale = "fr" });

        var created = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.StartsWith(
            RecordingGateway.PageOrigin + "/c/pay/",
            ApiJson.Text(created, "url"),
            StringComparison.Ordinal);
        var session = Assert.Single(App.Gateway.Sessions);
        Assert.Equal(("payment", "donate"), (session.Mode, session.SubmitType));
        Assert.Equal(1_500, session.LineItems[0].PriceData.UnitAmount);
        Assert.Equal(
            AccountsApp.SiteOrigin + "/fr/donate/success?session_id={CHECKOUT_SESSION_ID}",
            session.SuccessUrl);
        Assert.Equal(AccountsApp.SiteOrigin + "/fr/donate/cancel", session.CancelUrl);
        Assert.Null(session.ClientReferenceId);
        Assert.Equal("donation", session.Metadata["kind"]);
    }

    [Fact]
    public async Task SignedInDonorIsNamed()
    {
        var donor = await App.SeedUserAsync("Donatrice");
        using var browser = App.Browser();
        using var signedIn = await browser.SignInAsync(donor.UserName!);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);

        using var response = await browser.PostAsync(CheckoutPath, new { amountCents = 500 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = Assert.Single(App.Gateway.Sessions);
        Assert.Equal(donor.Id.ToString(CultureInfo.InvariantCulture), session.ClientReferenceId);
        Assert.Equal(AccountsApp.SiteOrigin + "/en/donate/cancel", session.CancelUrl);
    }

    [Fact]
    public async Task WithoutStripeTheFormIsClosed()
    {
        await using var closed = await BillingApp.StartAsync(postgres, configured: false);
        using var browser = closed.Browser();

        using var options = await browser.GetAsync(OptionsPath);
        using var checkout = await browser.PostAsync(CheckoutPath, new { amountCents = 500 });

        Assert.False(
            (await ApiJson.ReadAsync(options, HttpStatusCode.OK)).GetProperty("available")
                .GetBoolean());
        Assert.Equal(
            "payments-unavailable",
            await ApiJson.ProblemCodeAsync(checkout, HttpStatusCode.ServiceUnavailable));
        Assert.Empty(closed.Gateway.Sessions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(99L)]
    [InlineData(50_001L)]
    [InlineData(-500L)]
    public async Task AmountOutOfBoundsIsRefused(long? amountCents)
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(CheckoutPath, new { amountCents });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["invalid"], errors["amountCents"]);
        Assert.Empty(App.Gateway.Sessions);
    }

    [Fact]
    public async Task StripeDownAnswers502()
    {
        App.Gateway.Failing = true;
        using var browser = App.Browser();

        using var response = await browser.PostAsync(CheckoutPath, new { amountCents = 500 });

        Assert.Equal(
            "gateway-failed",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.BadGateway));
    }

    [Fact]
    public async Task CheckoutFromAnotherSiteIsRefused()
    {
        using var browser = App.Browser();
        using var request = browser.Post(CheckoutPath, new { amountCents = 500 });
        request.Headers.Remove(BrowserClient.OriginHeader);
        request.Headers.Add(BrowserClient.OriginHeader, "https://evil.example");

        using var response = await browser.SendAsync(request);

        Assert.Equal(
            "origin-mismatch",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Empty(App.Gateway.Sessions);
    }

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
        request.Headers.Add(ForwardedFor, address);
        return await browser.SendAsync(request);
    }
}
