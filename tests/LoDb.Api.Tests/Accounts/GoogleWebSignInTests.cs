using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// Google sign-in on the web, against a simulated Google: an unknown Google account gets an
/// account without a password, a verified e-mail links the account that uses it and an
/// unverified one never does, and the browser only ever returns to a page of the site.
/// </summary>
public sealed class GoogleWebSignInTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string Subject = "110248495921238986420";
    private const string GoogleAuthorization = "https://accounts.google.com/o/oauth2/v2/auth";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task UnknownGoogleAccountGetsAnAccountWithoutPassword()
    {
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = Subject,
            Email = "Elodie.Martin@gmail.test",
            GivenName = "Élodie",
        };
        using var browser = App.Browser();

        var target = await SignInWithGoogleAsync(browser, "locale=fr&returnUrl=/fr/champions");

        Assert.Equal("/fr/champions", target);
        var user = (await browser.SessionUserAsync())!.Value;
        Assert.Equal(
            ("Elodie", "elodie.martin@gmail.test", true, false),
            (ApiJson.Text(user, "username"), ApiJson.Text(user, "email"),
                user.GetProperty("emailVerified").GetBoolean(),
                user.GetProperty("hasPassword").GetBoolean()));
        Assert.Equal(Subject, (await App.FindAsync("Elodie")).GoogleId);
        var login = Assert.Single(await App.AuditAsync());
        Assert.Equal(AuditAction.UserLogin, login.Action);
        Assert.Equal(
            ("google", "session"),
            (AccountsApp.Meta(login, "method"), AccountsApp.Meta(login, "channel")));
    }

    [Fact]
    public async Task CodeIsExchangedWithTheWebClientAndItsVerifier()
    {
        App.GoogleServer.Account = NewAccount();
        using var browser = App.Browser();

        await SignInWithGoogleAsync(browser, "locale=en");

        var exchange = Assert.Single(App.GoogleServer.Exchanges);
        Assert.Equal(
            ("authorization_code", FakeGoogle.Code, AccountsApp.WebClientId,
                AccountsApp.WebClientSecret, "https://localhost/api/account/google/callback"),
            (exchange["grant_type"], exchange["code"], exchange["client_id"],
                exchange["client_secret"], exchange["redirect_uri"]));
        Assert.False(string.IsNullOrEmpty(exchange["code_verifier"]));
    }

    [Fact]
    public async Task UnverifiedEmailOfANewAccountStaysToVerify()
    {
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = Subject,
            Email = "new.player@gmail.test",
            EmailVerified = false,
        };
        using var browser = App.Browser();

        await SignInWithGoogleAsync(browser, "locale=en");

        var user = (await browser.SessionUserAsync())!.Value;
        Assert.Equal("new-player", ApiJson.Text(user, "username"));
        Assert.False(user.GetProperty("emailVerified").GetBoolean());
    }

    [Fact]
    public async Task VerifiedEmailLinksTheAccountThatUsesIt()
    {
        await App.SeedAsync(new AccountSeed());
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = Subject,
            Email = "LEGENDE@example.test",
        };
        using var browser = App.Browser();

        await SignInWithGoogleAsync(browser, "locale=en");

        var user = (await browser.SessionUserAsync())!.Value;
        Assert.Equal(AccountSeed.Name, ApiJson.Text(user, "username"));
        Assert.True(user.GetProperty("hasPassword").GetBoolean());
        Assert.Equal(Subject, (await App.FindAsync(AccountSeed.Name)).GoogleId);
    }

    [Fact]
    public async Task LinkingAnAccountNeverVerifiedDropsThePasswordOfWhoeverCreatedIt()
    {
        await App.SeedAsync(new AccountSeed { EmailConfirmed = false });
        App.GoogleServer.Account = SeededAccount();
        using var browser = App.Browser();
        using var squatter = App.Browser();

        await SignInWithGoogleAsync(browser, "locale=en");
        using var password = await squatter.SignInAsync(AccountSeed.Name);

        var user = (await browser.SessionUserAsync())!.Value;
        Assert.True(user.GetProperty("emailVerified").GetBoolean());
        Assert.False(user.GetProperty("hasPassword").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, password.StatusCode);
    }

    [Fact]
    public async Task UnverifiedEmailNeverTakesTheAccountThatUsesIt()
    {
        await App.SeedAsync(new AccountSeed());
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = Subject,
            Email = AccountSeed.Address,
            EmailVerified = false,
        };
        using var browser = App.Browser();

        var target = await SignInWithGoogleAsync(browser, "locale=de");

        Assert.Equal("/de/account/login?error=google-email-unverified", target);
        Assert.Null(await browser.SignedInAsAsync());
        Assert.Null((await App.FindAsync(AccountSeed.Name)).GoogleId);
        var refusal = Assert.Single(await App.AuditAsync());
        Assert.Equal(
            (AuditAction.UserLoginFailed, AccountSeed.Address, "google-email-unverified"),
            (refusal.Action,
                AccountsApp.Meta(refusal, "identifier"),
                AccountsApp.Meta(refusal, "reason")));
    }

    [Fact]
    public async Task KnownGoogleAccountSignsInWhateverItsEmailHasBecome()
    {
        await App.SeedAsync(new AccountSeed { GoogleId = Subject });
        App.GoogleServer.Account = NewAccount("new@gmail.test");
        using var browser = App.Browser();

        await SignInWithGoogleAsync(browser, "locale=en");

        Assert.Equal(AccountSeed.Name, await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task TakenNameGetsTheFirstFreeSuffix()
    {
        await App.SeedAsync(new AccountSeed { Username = "Elodie", Email = "elodie@example.test" });
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = Subject,
            Email = "e.martin@gmail.test",
            GivenName = "Élodie",
        };
        using var browser = App.Browser();

        await SignInWithGoogleAsync(browser, "locale=en");

        Assert.Equal("Elodie2", await browser.SignedInAsAsync());
    }

    [Theory]
    [InlineData(true, "account-banned")]
    [InlineData(false, "two-factor-required")]
    public async Task BannedOrTwoFactorAccountGoesBackToTheLoginPage(bool banned, string error)
    {
        await App.SeedAsync(new AccountSeed { GoogleId = Subject, Banned = banned });
        if (!banned)
        {
            await App.EnableTwoFactorAsync(AccountSeed.Name);
        }

        App.GoogleServer.Account = SeededAccount();
        using var browser = App.Browser();

        var target = await SignInWithGoogleAsync(browser, "locale=en");

        Assert.Equal($"/en/account/login?error={error}", target);
        Assert.Null(await browser.SignedInAsAsync());
    }

    [Theory]
    [InlineData("/en/items?tier=legendary", "/en/items?tier=legendary")]
    [InlineData("https://localhost/en/runes", "/en/runes")]
    [InlineData("https://evil.example/phish", "/en/account/profile")]
    [InlineData("//evil.example/phish", "/en/account/profile")]
    [InlineData("/\\evil.example/phish", "/en/account/profile")]
    [InlineData("javascript:alert(1)", "/en/account/profile")]
    public async Task BrowserOnlyReturnsToAPageOfTheSite(string returnUrl, string expected)
    {
        App.GoogleServer.Account = NewAccount();
        using var browser = App.Browser();

        var target = await SignInWithGoogleAsync(
            browser,
            $"locale=en&returnUrl={Uri.EscapeDataString(returnUrl)}");

        Assert.Equal(expected, target);
    }

    [Fact]
    public async Task DeclinedConsentGoesBackToTheLoginPage()
    {
        using var browser = App.Browser();
        var state = await StartAsync(browser, "locale=fr");

        using var callback = await browser.GetAsync(
            $"/api/account/google/callback?error=access_denied&state={state}");

        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Equal(
            "/fr/account/login?error=google-cancelled",
            callback.Headers.Location?.OriginalString);
        Assert.Empty(App.GoogleServer.Exchanges);
    }

    [Fact]
    public async Task RefusedCodeGoesBackToTheLoginPage()
    {
        using var browser = App.Browser();
        var state = await StartAsync(browser, "locale=en");

        using var callback = await browser.GetAsync(
            $"/api/account/google/callback?code=stolen&state={state}");

        Assert.Equal(
            "/en/account/login?error=google-failed",
            callback.Headers.Location?.OriginalString);
        Assert.Null(await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task RememberMeKeepsTheGoogleSessionThirtyDays()
    {
        App.GoogleServer.Account = NewAccount();
        using var browser = App.Browser();
        var state = await StartAsync(browser, "locale=en&rememberMe=true");

        using var callback = await browser.GetAsync(
            $"/api/account/google/callback?code={FakeGoogle.Code}&state={state}");

        var session = Microsoft.Net.Http.Headers.SetCookieHeaderValue
            .ParseList([.. callback.Headers.GetValues("Set-Cookie")])
            .Single(static cookie => cookie.Name == BrowserClient.SessionCookie);
        Assert.InRange(
            session.Expires!.Value,
            App.Clock.GetUtcNow().AddDays(30).AddMinutes(-1),
            App.Clock.GetUtcNow().AddDays(30).AddMinutes(1));
    }

    [Fact]
    public async Task WithoutAClientIdGoogleIsUnavailable()
    {
        await using var app = await AccountsApp.StartAsync(postgres, withGoogle: false);
        using var browser = app.Browser();

        using var start = await browser.GetAsync("/api/account/google/start?locale=it");

        Assert.Equal(HttpStatusCode.Found, start.StatusCode);
        Assert.Equal(
            "/it/account/login?error=google-unavailable",
            start.Headers.Location?.OriginalString);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static GoogleAccount NewAccount(string email = "a@gmail.test") =>
        new() { Subject = Subject, Email = email };

    private static GoogleAccount SeededAccount() => NewAccount(AccountSeed.Address);

    // The start sends the browser to Google, with the state Google hands back.
    private static async Task<string> StartAsync(BrowserClient browser, string query)
    {
        using var start = await browser.GetAsync($"/api/account/google/start?{query}");
        Assert.Equal(HttpStatusCode.Found, start.StatusCode);
        var google = start.Headers.Location!;
        Assert.Equal(GoogleAuthorization, google.GetLeftPart(UriPartial.Path));
        return Uri.EscapeDataString(QueryHelpers.ParseQuery(google.Query)["state"].ToString());
    }

    // Google consents at once and sends the browser back with its code.
    private static async Task<string> SignInWithGoogleAsync(BrowserClient browser, string query)
    {
        var state = await StartAsync(browser, query);
        using var callback = await browser.GetAsync(
            $"/api/account/google/callback?code={FakeGoogle.Code}&state={state}");
        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        return callback.Headers.Location!.OriginalString;
    }
}
