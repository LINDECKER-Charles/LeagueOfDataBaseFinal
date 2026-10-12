using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Outbox;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts.OwnHost;

/// <summary>
/// The forgotten password over time: one e-mail an hour per account, requests sent together
/// included, five requests an hour per address, the check of a link limited apart, and the
/// open sessions a reset closes at their next revalidation.
/// </summary>
/// <remarks>A host per test: each reaches a limit or advances its clock.</remarks>
public sealed class PasswordResetLimitTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string ForgotPath = "/api/account/forgot-password";
    private const string ResetPath = "/api/account/reset-password";
    private const string CheckPath = "/api/account/reset-password/check";
    private const string NewPassword = "N0uvelle-phrase!";
    private const int RequestsPerHour = 5;
    private const int ChecksPerHour = 30;

    // One request each from as many addresses, far more than the e-mails one hour allows.
    private const int StrangersAtOnce = 20;

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task AccountGetsOneEmailAnHour()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();

        using var first = await ForgotAsync(browser, AccountSeed.Address);
        App.Clock.Advance(TimeSpan.FromMinutes(59));
        using var tooSoon = await ForgotAsync(browser, AccountSeed.Address);
        var sentWithinTheHour = App.Outbox.Messages.Count;
        App.Clock.Advance(TimeSpan.FromMinutes(1));
        using var anHourLater = await ForgotAsync(browser, AccountSeed.Address);

        Assert.Equal(HttpStatusCode.Accepted, tooSoon.StatusCode);
        Assert.Equal(1, sentWithinTheHour);
        Assert.Equal(2, App.Outbox.Sent(EmailTemplate.ResetPassword).Count);
    }

    [Fact]
    public async Task RequestsSentTogetherStillGetTheAccountOneEmailAnHour()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var first = await ForgotAsync(browser, AccountSeed.Address);
        App.Clock.Advance(TimeSpan.FromHours(1));
        var strangers = Enumerable.Range(1, StrangersAtOnce)
            .Select(index => App.Browser().From($"198.51.100.{index}"))
            .ToList();

        var together = await Task.WhenAll(
            strangers.Select(static stranger => ForgotAsync(stranger, AccountSeed.Address)));
        var statuses = together.Select(static response => response.StatusCode).ToList();
        foreach (var disposable in together.Concat<IDisposable>(strangers))
        {
            disposable.Dispose();
        }

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.Accepted, status));
        Assert.Equal(2, App.Outbox.Sent(EmailTemplate.ResetPassword).Count);
    }

    [Fact]
    public async Task ResetClosesTheOpenSessions()
    {
        await App.SeedAsync(new AccountSeed());
        using var session = App.Browser();
        using var signIn = await session.SignInAsync(AccountSeed.Name, rememberMe: true);
        using var browser = App.Browser();
        using var forgot = await ForgotAsync(browser, AccountSeed.Address);

        using var reset = await browser.PostAsync(ResetPath, Reset(
            App.Outbox.LastLink(EmailTemplate.ResetPassword)));
        App.Clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Null(await session.SignedInAsAsync());
    }

    [Fact]
    public async Task OneAddressAsksFiveTimesAnHour()
    {
        using var browser = App.Browser();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt <= RequestsPerHour; attempt++)
        {
            using var response = await ForgotAsync(browser, "nobody@example.test");
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(
            [.. Enumerable.Repeat(HttpStatusCode.Accepted, RequestsPerHour),
                HttpStatusCode.TooManyRequests],
            statuses);
    }

    [Fact]
    public async Task CheckHasItsOwnHourlyLimitApartFromTheRequests()
    {
        using var browser = App.Browser();
        var damaged = new EmailLink(AccountsApp.BaseAddress, UserId: 1, Token: "bm9wZQ");
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt <= ChecksPerHour; attempt++)
        {
            using var response = await browser.PostAsync(
                CheckPath,
                new { userId = damaged.UserId, token = damaged.Token });
            statuses.Add(response.StatusCode);
        }

        using var request = await ForgotAsync(browser, "nobody@example.test");
        Assert.Equal(
            [.. Enumerable.Repeat(HttpStatusCode.BadRequest, ChecksPerHour),
                HttpStatusCode.TooManyRequests],
            statuses);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static Task<HttpResponseMessage> ForgotAsync(BrowserClient browser, string email) =>
        browser.PostAsync(ForgotPath, new { email });

    private static object Reset(EmailLink link) =>
        new { userId = link.UserId, token = link.Token, password = NewPassword };
}
