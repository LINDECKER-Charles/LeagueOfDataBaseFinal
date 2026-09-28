using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// <c>POST /api/account/login</c>: by e-mail or username whatever the case, the same answer
/// for a wrong password and an unknown account, a banned account refused only once its
/// password is right, a lock after five failures, and the second factor when it is on.
/// </summary>
public sealed class SignInTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string WrongPassword = "Wr0ng-passphrase!";
    private const int FailuresToLock = 5;
    private const int LockoutSeconds = 15 * 60;

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Theory]
    [InlineData("Legende_42")]
    [InlineData("LEGENDE_42")]
    [InlineData("legende@example.test")]
    [InlineData("  Legende@Example.TEST ")]
    public async Task SignsInByUsernameOrEmailWhateverTheCase(string identifier)
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();

        using var response = await browser.SignInAsync(identifier);

        var session = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        var user = session.GetProperty("user");
        Assert.Equal(AccountSeed.Name, ApiJson.Text(user, "username"));
        Assert.Equal(AccountSeed.Address, ApiJson.Text(user, "email"));
        Assert.True(user.GetProperty("hasPassword").GetBoolean());
        Assert.False(user.GetProperty("multiFactor").GetBoolean());
        Assert.NotNull(browser.Cookie(BrowserClient.SessionCookie));
        Assert.Equal(AccountSeed.Name, await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task WrongPasswordAndUnknownAccountGetTheSameAnswer()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();

        using var wrong = await browser.SignInAsync(AccountSeed.Name, WrongPassword);
        using var unknown = await browser.SignInAsync("nobody@example.test", WrongPassword);

        Assert.Equal(
            "invalid-credentials",
            await ApiJson.ProblemCodeAsync(wrong, HttpStatusCode.Unauthorized));
        Assert.Equal(
            "invalid-credentials",
            await ApiJson.ProblemCodeAsync(unknown, HttpStatusCode.Unauthorized));
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
    }

    [Fact]
    public async Task BlankFieldsAreReportedTogether()
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(
            "/api/account/login",
            new { identifier = " ", password = "" });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["required"], errors["identifier"]);
        Assert.Equal(["required"], errors["password"]);
    }

    [Fact]
    public async Task BannedAccountIsRefusedOnlyOnceItsPasswordIsRight()
    {
        await App.SeedAsync(new AccountSeed { Banned = true });
        using var browser = App.Browser();

        using var wrong = await browser.SignInAsync(AccountSeed.Name, WrongPassword);
        using var right = await browser.SignInAsync(AccountSeed.Name);

        Assert.Equal(
            "invalid-credentials",
            await ApiJson.ProblemCodeAsync(wrong, HttpStatusCode.Unauthorized));
        Assert.Equal(
            "account-banned",
            await ApiJson.ProblemCodeAsync(right, HttpStatusCode.Forbidden));
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
        Assert.Null(await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task FifthFailureLocksTheAccountEvenForItsPassword()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        var statuses = new List<HttpStatusCode>();

        for (var failure = 0; failure < FailuresToLock; failure++)
        {
            using var attempt = await browser.SignInAsync(AccountSeed.Name, WrongPassword);
            statuses.Add(attempt.StatusCode);
        }

        using var right = await browser.SignInAsync(AccountSeed.Name);

        Assert.Equal(
            [.. Enumerable.Repeat(HttpStatusCode.Unauthorized, FailuresToLock - 1),
                HttpStatusCode.TooManyRequests],
            statuses);
        var retryAfter = right.Headers.RetryAfter?.Delta?.TotalSeconds;
        Assert.Equal(
            "account-locked",
            await ApiJson.ProblemCodeAsync(right, HttpStatusCode.TooManyRequests));
        Assert.InRange(retryAfter ?? 0, LockoutSeconds - 60, LockoutSeconds);
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
    }

    [Fact]
    public async Task TwoFactorAccountNeedsTheCodeOfItsAuthenticator()
    {
        await App.SeedAsync(new AccountSeed());
        var secrets = await App.EnableTwoFactorAsync(AccountSeed.Name);
        using var browser = App.Browser();

        using var passwordOnly = await browser.SignInAsync(AccountSeed.Name);
        using var wrongCode = await SignInWithAsync(
            browser,
            twoFactorCode: Totp.WrongCode(secrets.Key));
        using var rightCode = await SignInWithAsync(browser, twoFactorCode: Totp.Code(secrets.Key));

        Assert.Equal(
            "two-factor-required",
            await ApiJson.ProblemCodeAsync(passwordOnly, HttpStatusCode.Unauthorized));
        Assert.Equal(
            "invalid-two-factor-code",
            await ApiJson.ProblemCodeAsync(wrongCode, HttpStatusCode.Unauthorized));
        var user = (await ApiJson.ReadAsync(rightCode, HttpStatusCode.OK)).GetProperty("user");
        Assert.True(user.GetProperty("twoFactorEnabled").GetBoolean());
        Assert.True(user.GetProperty("multiFactor").GetBoolean());
    }

    [Fact]
    public async Task RecoveryCodeSignsInOnceWhateverItsCase()
    {
        await App.SeedAsync(new AccountSeed());
        var secrets = await App.EnableTwoFactorAsync(AccountSeed.Name);
        var code = $" {secrets.RecoveryCodes[0].ToLowerInvariant()} ";
        using var first = App.Browser();
        using var second = App.Browser();

        using var used = await SignInWithAsync(first, recoveryCode: code);
        using var reused = await SignInWithAsync(second, recoveryCode: code);

        var user = (await ApiJson.ReadAsync(used, HttpStatusCode.OK)).GetProperty("user");
        Assert.True(user.GetProperty("multiFactor").GetBoolean());
        Assert.Equal(
            "invalid-two-factor-code",
            await ApiJson.ProblemCodeAsync(reused, HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task SignInsAreAudited()
    {
        var account = await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();

        using var failed = await browser.SignInAsync("Legende@Example.test", WrongPassword);
        using var succeeded = await browser.SignInAsync(AccountSeed.Name);

        var trail = await App.AuditAsync();
        Assert.Equal(
            [AuditAction.UserLoginFailed, AuditAction.UserLogin],
            trail.Select(static entry => entry.Action));
        var failure = trail[0];
        Assert.Equal(
            (AuditOutcome.Failure, AuditActorType.Anonymous, (int?)null),
            (failure.Outcome, failure.ActorType, failure.ActorId));
        Assert.Equal("Legende@Example.test", AccountsApp.Meta(failure, "identifier"));
        Assert.Equal("invalid-credentials", AccountsApp.Meta(failure, "reason"));
        var success = trail[1];
        Assert.Equal(
            (AuditOutcome.Success, AuditActorType.User, (int?)account.Id),
            (success.Outcome, success.ActorType, success.ActorId));
        Assert.Equal("password", AccountsApp.Meta(success, "method"));
        Assert.Equal("session", AccountsApp.Meta(success, "channel"));
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static Task<HttpResponseMessage> SignInWithAsync(
        BrowserClient browser,
        string? twoFactorCode = null,
        string? recoveryCode = null) =>
        browser.PostAsync("/api/account/login", new
        {
            identifier = AccountSeed.Name,
            password = AccountSeed.StrongPassword,
            twoFactorCode,
            recoveryCode,
        });
}
