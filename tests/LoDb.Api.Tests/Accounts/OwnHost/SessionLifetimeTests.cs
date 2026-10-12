using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;
using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Tests.Accounts.OwnHost;

/// <summary>
/// The session as time passes: one day without "remember me" and thirty with it, ended by a
/// ban at its next revalidation, the ban flag alone included, and its second factor kept
/// across a revalidation.
/// </summary>
/// <remarks>A host per test: each advances its clock.</remarks>
public sealed class SessionLifetimeTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private static readonly TimeSpan PastRevalidation = TimeSpan.FromMinutes(6);

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task RememberMeKeepsTheSessionThirtyDaysAndOtherwiseADay()
    {
        await App.SeedAsync(new AccountSeed());
        using var remembered = App.Browser();
        using var browsing = App.Browser();
        var signedInAt = App.Clock.GetUtcNow();

        using var rememberedSignIn = await remembered.SignInAsync(
            AccountSeed.Name,
            rememberMe: true);
        using var browsingSignIn = await browsing.SignInAsync(AccountSeed.Name);
        var expires = SessionCookie(rememberedSignIn).Expires;
        App.Clock.Advance(TimeSpan.FromHours(25));
        var afterADay = (await remembered.SignedInAsAsync(), await browsing.SignedInAsAsync());
        App.Clock.Advance(TimeSpan.FromDays(31));

        Assert.InRange(
            expires!.Value,
            signedInAt.AddDays(30).AddMinutes(-1),
            signedInAt.AddDays(30).AddMinutes(1));
        Assert.Equal((AccountSeed.Name, (string?)null), afterADay);
        Assert.Null(await remembered.SignedInAsAsync());
    }

    [Fact]
    public async Task BanEndsAnOpenSessionThenItsCookieAtItsNextRevalidation()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name, rememberMe: true);

        await App.BanAsync(AccountSeed.Name);
        var rightAfterTheBan = await browser.SignedInAsAsync();
        var cookieBeforeRevalidation = browser.Cookie(BrowserClient.SessionCookie);
        App.Clock.Advance(PastRevalidation);
        var afterRevalidation = await browser.SignedInAsAsync();

        Assert.Null(rightAfterTheBan);
        Assert.NotNull(cookieBeforeRevalidation);
        Assert.Null(afterRevalidation);
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
    }

    // The ban alone, without the new stamp the admin also writes: the revalidation of the
    // cookie and the refresh of the tokens read it all the same.
    [Fact]
    public async Task BanFlagAloneEndsTheCookieAtRevalidationAndTheRefreshTokens()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name, rememberMe: true);
        using var app = App.App();
        var tokens = await AppTokens.SignInAsync(app);
        var stamp = (await App.FindAsync(AccountSeed.Name)).SecurityStamp;

        await App.UsersAsync(async users =>
        {
            var user = await users.FindByNameAsync(AccountSeed.Name);
            user!.IsBanned = true;
            return await users.UpdateAsync(user);
        });
        App.Clock.Advance(PastRevalidation);
        var afterRevalidation = await browser.SignedInAsAsync();
        using var refresh = await AppTokens.RefreshAsync(app, tokens);

        Assert.Equal(stamp, (await App.FindAsync(AccountSeed.Name)).SecurityStamp);
        Assert.Null(afterRevalidation);
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
        Assert.Equal(
            "invalid-refresh-token",
            await ApiJson.ProblemCodeAsync(refresh, HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task RevalidatedSessionKeepsItsSecondFactor()
    {
        await App.SeedAsync(new AccountSeed());
        var secrets = await App.EnableTwoFactorAsync(AccountSeed.Name);
        using var browser = App.Browser();
        using var signIn = await browser.PostAsync("/api/account/login", new
        {
            identifier = AccountSeed.Name,
            password = AccountSeed.StrongPassword,
            twoFactorCode = Totp.Code(secrets.Key, App.Clock),
        });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        App.Clock.Advance(PastRevalidation);
        var user = await browser.SessionUserAsync();

        Assert.True(user?.GetProperty("multiFactor").GetBoolean());
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static SetCookieHeaderValue SessionCookie(HttpResponseMessage response) =>
        SetCookieHeaderValue.ParseList([.. response.Headers.GetValues(HeaderNames.SetCookie)])
            .Single(static cookie => cookie.Name == BrowserClient.SessionCookie);
}
