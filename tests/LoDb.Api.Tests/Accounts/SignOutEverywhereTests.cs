using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// <c>/api/account/logout-everywhere</c>: the session of the request closes at once, every
/// other session cookie at its next revalidation, and no refresh token of the account is
/// traded any more, whether the call comes from the web or from an app. That it needs a
/// session is <see cref="SessionTests"/>'.
/// </summary>
/// <remarks>A host per test: each advances its clock or reads a meter.</remarks>
public sealed class SignOutEverywhereTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string Path = "/api/account/logout-everywhere";
    private const string RevokedCounter = "lodb.sessions.revoked";
    private static readonly TimeSpan BeforeRevalidation = TimeSpan.FromMinutes(4);
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
        App.Clock.Advance(BeforeRevalidation);
        var elsewhereBeforeRevalidation = await elsewhere.SignedInAsAsync();
        App.Clock.Advance(PastRevalidation - BeforeRevalidation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(here.Cookie(BrowserClient.SessionCookie));
        Assert.Null(await here.SignedInAsAsync());
        Assert.Equal(
            (AccountSeed.Name, AccountSeed.Name),
            (elsewhereRightAfter, elsewhereBeforeRevalidation));
        Assert.Null(await elsewhere.SignedInAsAsync());
        Assert.Null(elsewhere.Cookie(BrowserClient.SessionCookie));
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

    // The new security stamp alone refuses every refresh token: the families are read in the
    // table, where only the revocation shows.
    [Fact]
    public async Task EveryRefreshTokenFamilyOfTheAccountIsRevoked()
    {
        var account = await App.SeedAsync(new AccountSeed());
        var other = await App.SeedAsync(
            new AccountSeed { Username = "Autre_7", Email = "autre@example.test" });
        using var revoked = App.Security<long>(RevokedCounter);
        using var browser = App.Browser();
        using var phone = App.App();
        using var desktop = App.App();
        using var otherPhone = App.App();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        await AppTokens.SignInAsync(phone);
        await AppTokens.SignInAsync(desktop);
        await AppTokens.SignInAsync(otherPhone, other.UserName!);

        using var response = await browser.PostAsync(Path);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            [(account.Id, true), (account.Id, true), (other.Id, false)],
            await FamiliesAsync());
        var counted = Assert.Single(revoked.GetMeasurementSnapshot());
        Assert.Equal("sign_out_everywhere", counted.Tags["reason"]);
    }

    public async ValueTask InitializeAsync() =>
        _app = await AccountsApp.StartAsync(postgres, withGoogle: false);

    // Each family as its account and whether it is revoked, the account's first.
    private async Task<List<(int Account, bool IsRevoked)>> FamiliesAsync()
    {
        await using var database = App.Database();
        var families = await database.RefreshTokenFamilies.AsNoTracking()
            .OrderBy(static family => family.UserId)
            .Select(static family => new { family.UserId, family.RevokedAt })
            .ToListAsync(TestContext.Current.CancellationToken);
        return [.. families.Select(static family => (family.UserId, family.RevokedAt != null))];
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
