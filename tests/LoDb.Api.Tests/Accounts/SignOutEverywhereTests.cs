using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// <c>/api/account/logout-everywhere</c>: the session of the request closes at once, every
/// other session cookie at its next revalidation, and no refresh token of the account is
/// traded any more, whether the call comes from the web or from an app.
/// </summary>
public sealed class SignOutEverywhereTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string Path = "/api/account/logout-everywhere";
    private static readonly TimeSpan PastRevalidation = TimeSpan.FromMinutes(6);

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task EverySessionEndsTheOneOfTheRequestAtOnce()
    {
        var account = await App.SeedAsync(new AccountSeed());
        using var here = App.Browser();
        using var elsewhere = App.Browser();
        using var app = App.App();
        using var hereSignIn = await here.SignInAsync(AccountSeed.Name, rememberMe: true);
        using var elsewhereSignIn = await elsewhere.SignInAsync(AccountSeed.Name, rememberMe: true);
        var tokens = await AppTokens.SignInAsync(app);

        using var response = await here.PostAsync(Path);
        var elsewhereRightAfter = await elsewhere.SignedInAsAsync();
        App.Clock.Advance(PastRevalidation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(here.Cookie(BrowserClient.SessionCookie));
        Assert.Null(await here.SignedInAsAsync());
        Assert.Equal(AccountSeed.Name, elsewhereRightAfter);
        Assert.Null(await elsewhere.SignedInAsAsync());
        using var refresh = await AppTokens.RefreshAsync(app, tokens);
        Assert.Equal(
            "invalid-refresh-token",
            await ApiJson.ProblemCodeAsync(refresh, HttpStatusCode.Unauthorized));
        var entry = (await App.AuditAsync())[^1];
        Assert.Equal(
            (AuditAction.UserLogout, (int?)account.Id, "everywhere"),
            (entry.Action, entry.ActorId, AccountsApp.Meta(entry, "scope")));
    }

    [Fact]
    public async Task AnAppSignsOutEverySessionToo()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var app = App.App();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        var tokens = await AppTokens.SignInAsync(app);

        using var response = await AppTokens.SendAsync(
            app,
            HttpMethod.Post,
            "logout-everywhere",
            tokens);
        App.Clock.Advance(PastRevalidation);
        using var refresh = await AppTokens.RefreshAsync(app, tokens);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "invalid-refresh-token",
            await ApiJson.ProblemCodeAsync(refresh, HttpStatusCode.Unauthorized));
        Assert.Null(await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task ItNeedsASessionAndItsXsrfToken()
    {
        await App.SeedAsync(new AccountSeed());
        using var anonymous = App.Browser();
        using var forged = App.Browser();
        using var signIn = await forged.SignInAsync(AccountSeed.Name);
        using var request = forged.Post(Path);
        request.Headers.Remove(BrowserClient.XsrfHeader);

        using var withoutSession = await anonymous.PostAsync(Path);
        using var withoutToken = await forged.SendAsync(request);

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(withoutSession, HttpStatusCode.Unauthorized));
        Assert.Equal(
            "xsrf-invalid",
            await ApiJson.ProblemCodeAsync(withoutToken, HttpStatusCode.Forbidden));
        Assert.Equal(AccountSeed.Name, await forged.SignedInAsAsync());
    }

    [Fact]
    public async Task AnotherSessionDropsAtItsRevalidationAndNotBefore()
    {
        await App.SeedAsync(new AccountSeed());
        using var here = App.Browser();
        using var elsewhere = App.Browser();
        using var hereSignIn = await here.SignInAsync(AccountSeed.Name);
        using var elsewhereSignIn = await elsewhere.SignInAsync(AccountSeed.Name);

        using var response = await here.PostAsync(Path);
        App.Clock.Advance(PastRevalidation - TimeSpan.FromMinutes(2));
        var beforeRevalidation = await elsewhere.SignedInAsAsync();
        App.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(AccountSeed.Name, beforeRevalidation);
        Assert.Null(await elsewhere.SignedInAsAsync());
        Assert.Null(elsewhere.Cookie(BrowserClient.SessionCookie));
    }

    public async ValueTask InitializeAsync() =>
        _app = await AccountsApp.StartAsync(postgres, withGoogle: false);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
