using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// Google sign-in on the web, against a simulated Google: an unknown Google account gets an
/// account without a password, a verified e-mail links the account that uses it and an
/// unverified one never does, and the browser only ever returns to a page of the site.
/// </summary>
/// <remarks>
/// The tests share a host: each signs in with a Google account, an e-mail and an account of
/// its own, and Google starts each test anew.
/// </remarks>
public sealed class GoogleWebSignInTests : IClassFixture<SharedAccountsApp>
{
    private const string GoogleAuthorization = "https://accounts.google.com/o/oauth2/v2/auth";

    private readonly AccountSeed _account;
    private readonly string _subject;
    private readonly string _gmail;

    public GoogleWebSignInTests(SharedAccountsApp host)
    {
        ArgumentNullException.ThrowIfNull(host);
        App = host.App;
        App.GoogleServer.Reset();
        _account = host.Unique();
        var number = host.NextNumber();
        _subject = string.Create(CultureInfo.InvariantCulture, $"1102484959{number:D11}");
        _gmail = string.Create(CultureInfo.InvariantCulture, $"joueur{number}@gmail.test");
    }

    private AccountsApp App { get; }

    [Fact]
    public async Task UnknownGoogleAccountGetsAnAccountWithoutPassword()
    {
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = _subject,
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
        Assert.Equal(_subject, (await App.FindAsync("Elodie")).GoogleId);
        var login = Assert.Single(await App.AuditOfAsync("Elodie"));
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
            Subject = _subject,
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
        await App.SeedAsync(_account);
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = _subject,
            Email = _account.Email.ToUpperInvariant(),
        };
        using var browser = App.Browser();

        await SignInWithGoogleAsync(browser, "locale=en");

        var user = (await browser.SessionUserAsync())!.Value;
        Assert.Equal(_account.Username, ApiJson.Text(user, "username"));
        Assert.True(user.GetProperty("hasPassword").GetBoolean());
        Assert.Equal(_subject, (await App.FindAsync(_account.Username)).GoogleId);
    }

    [Fact]
    public async Task LinkingAnAccountNeverVerifiedDropsThePasswordOfWhoeverCreatedIt()
    {
        await App.SeedAsync(_account with { EmailConfirmed = false });
        App.GoogleServer.Account = SeededAccount();
        using var browser = App.Browser();
        using var squatter = App.Browser();

        await SignInWithGoogleAsync(browser, "locale=en");
        using var password = await squatter.SignInAsync(_account.Username);

        var user = (await browser.SessionUserAsync())!.Value;
        Assert.True(user.GetProperty("emailVerified").GetBoolean());
        Assert.False(user.GetProperty("hasPassword").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, password.StatusCode);
    }

    [Fact]
    public async Task UnverifiedEmailNeverTakesTheAccountThatUsesIt()
    {
        await App.SeedAsync(_account);
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = _subject,
            Email = _account.Email,
            EmailVerified = false,
        };
        using var browser = App.Browser();

        var target = await SignInWithGoogleAsync(browser, "locale=de");

        Assert.Equal("/de/account/login?error=google-email-unverified", target);
        Assert.Null(await browser.SignedInAsAsync());
        Assert.Null((await App.FindAsync(_account.Username)).GoogleId);
        var refusal = Assert.Single(await App.AuditOfAsync(_account.Username, _account.Email));
        Assert.Equal(
            (AuditAction.UserLoginFailed,
                AccountsApp.IdentifierDigest(_account.Email),
                "google-email-unverified"),
            (refusal.Action,
                AccountsApp.Meta(refusal, "identifier_digest"),
                AccountsApp.Meta(refusal, "reason")));
        Assert.Null(AccountsApp.Meta(refusal, "identifier"));
    }

    [Fact]
    public async Task KnownGoogleAccountSignsInWhateverItsEmailHasBecome()
    {
        await App.SeedAsync(_account with { GoogleId = _subject });
        App.GoogleServer.Account = NewAccount();
        using var browser = App.Browser();

        await SignInWithGoogleAsync(browser, "locale=en");

        Assert.Equal(_account.Username, await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task TakenNameGetsTheFirstFreeSuffix()
    {
        await App.SeedAsync(_account with { Username = "Maelle" });
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = _subject,
            Email = "m.durand@gmail.test",
            GivenName = "Maëlle",
        };
        using var browser = App.Browser();

        await SignInWithGoogleAsync(browser, "locale=en");

        Assert.Equal("Maelle2", await browser.SignedInAsAsync());
    }

    [Theory]
    [InlineData(true, "account-banned")]
    [InlineData(false, "two-factor-required")]
    public async Task BannedOrTwoFactorAccountGoesBackToTheLoginPage(bool banned, string error)
    {
        await App.SeedAsync(_account with { GoogleId = _subject, Banned = banned });
        if (!banned)
        {
            await App.EnableTwoFactorAsync(_account.Username);
        }

        App.GoogleServer.Account = SeededAccount();
        using var browser = App.Browser();

        var target = await SignInWithGoogleAsync(browser, "locale=en");

        Assert.Equal($"/en/account/login?error={error}", target);
        Assert.Null(await browser.SignedInAsAsync());
    }

    // The other pages a sign-in may or may not return to are SafeReturnUrlTests'.
    [Fact]
    public async Task BrowserOnlyReturnsToAPageOfTheSite()
    {
        App.GoogleServer.Account = NewAccount();
        using var browser = App.Browser();

        var target = await SignInWithGoogleAsync(
            browser,
            $"locale=en&returnUrl={Uri.EscapeDataString("https://evil.example/phish")}");

        Assert.Equal("/en/account/profile", target);
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

    // A Google account of this test only, its e-mail used by no account yet.
    private GoogleAccount NewAccount() => new() { Subject = _subject, Email = _gmail };

    private GoogleAccount SeededAccount() => new() { Subject = _subject, Email = _account.Email };
}
