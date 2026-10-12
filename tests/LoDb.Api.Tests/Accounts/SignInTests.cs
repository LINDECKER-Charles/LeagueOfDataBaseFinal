using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// <c>POST /api/account/login</c>: by e-mail or username whatever the case, the same answer
/// for a wrong password and an unknown account, a banned account refused only once its
/// password is right, and the second factor when it is on. The lockout is
/// <see cref="OwnHost.SignInLockoutTests"/>'.
/// </summary>
/// <remarks>The tests share a host, each with an account of its own.</remarks>
public sealed class SignInTests(SharedAccountsApp host) : IClassFixture<SharedAccountsApp>
{
    private const string WrongPassword = "Wr0ng-passphrase!";

    // The digits of an authenticator code before the apps' separator.
    private const int CodeGroup = 3;

    private readonly AccountSeed _account = host.Unique();

    private AccountsApp App { get; } = host.App;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignsInByUsernameOrEmailWhateverTheCase(bool byEmail)
    {
        await App.SeedAsync(_account);
        using var browser = App.Browser();
        var identifier = byEmail
            ? $"  {_account.Email.ToUpperInvariant()} "
            : _account.Username.ToUpperInvariant();

        using var response = await browser.SignInAsync(identifier);

        var session = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        var user = session.GetProperty("user");
        Assert.Equal(_account.Username, ApiJson.Text(user, "username"));
        Assert.Equal(_account.Email, ApiJson.Text(user, "email"));
        Assert.True(user.GetProperty("hasPassword").GetBoolean());
        Assert.False(user.GetProperty("multiFactor").GetBoolean());
        Assert.NotNull(browser.Cookie(BrowserClient.SessionCookie));
        Assert.Equal(_account.Username, await browser.SignedInAsAsync());
    }

    [Fact]
    public async Task WrongPasswordAndUnknownAccountGetTheSameAnswer()
    {
        await App.SeedAsync(_account);
        using var browser = App.Browser();

        using var wrong = await browser.SignInAsync(_account.Username, WrongPassword);
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
        await App.SeedAsync(_account with { Banned = true });
        using var browser = App.Browser();

        using var wrong = await browser.SignInAsync(_account.Username, WrongPassword);
        using var right = await browser.SignInAsync(_account.Username);

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
    public async Task TwoFactorAccountNeedsTheCodeOfItsAuthenticator()
    {
        await App.SeedAsync(_account);
        var secrets = await App.EnableTwoFactorAsync(_account.Username);
        using var browser = App.Browser();

        using var passwordOnly = await browser.SignInAsync(_account.Username);
        using var wrongCode = await SignInWithAsync(
            browser,
            twoFactorCode: Totp.WrongCode(secrets.Key, App.Clock));
        using var rightCode = await SignInWithAsync(
            browser,
            twoFactorCode: Totp.Code(secrets.Key, App.Clock));

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

    // As the authenticator apps show their codes, in two groups; the separators the field
    // accepts are SignInFieldShapeTests'.
    [Fact]
    public async Task CodeGroupedAsTheAppShowsItSignsIn()
    {
        await App.SeedAsync(_account);
        var secrets = await App.EnableTwoFactorAsync(_account.Username);
        var code = Totp.Code(secrets.Key, App.Clock);
        using var browser = App.Browser();

        using var signIn = await SignInWithAsync(
            browser,
            twoFactorCode: code[..CodeGroup] + " " + code[CodeGroup..]);

        var user = (await ApiJson.ReadAsync(signIn, HttpStatusCode.OK)).GetProperty("user");
        Assert.True(user.GetProperty("multiFactor").GetBoolean());
    }

    [Fact]
    public async Task RecoveryCodeSignsInOnceWhateverItsCase()
    {
        await App.SeedAsync(_account);
        var secrets = await App.EnableTwoFactorAsync(_account.Username);
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
        var account = await App.SeedAsync(_account);
        using var browser = App.Browser();

        using var failed = await browser.SignInAsync(
            _account.Email.ToUpperInvariant(),
            WrongPassword);
        using var succeeded = await browser.SignInAsync(_account.Username);

        var trail = await App.AuditOfAsync(_account.Username, _account.Email);
        Assert.Equal(
            [AuditAction.UserLoginFailed, AuditAction.UserLogin],
            trail.Select(static entry => entry.Action));
        var failure = trail[0];
        Assert.Equal(
            (AuditOutcome.Failure, AuditActorType.Anonymous, (int?)null),
            (failure.Outcome, failure.ActorType, failure.ActorId));
        Assert.Null(AccountsApp.Meta(failure, "identifier"));
        Assert.Equal(
            ("email", AccountsApp.IdentifierDigest(_account.Email), "invalid-credentials"),
            (AccountsApp.Meta(failure, "identifier_kind"),
                AccountsApp.Meta(failure, "identifier_digest"),
                AccountsApp.Meta(failure, "reason")));
        var success = trail[1];
        Assert.Equal(
            (AuditOutcome.Success, AuditActorType.User, (int?)account.Id),
            (success.Outcome, success.ActorType, success.ActorId));
        Assert.Equal("password", AccountsApp.Meta(success, "method"));
        Assert.Equal("session", AccountsApp.Meta(success, "channel"));
    }

    private Task<HttpResponseMessage> SignInWithAsync(
        BrowserClient browser,
        string? twoFactorCode = null,
        string? recoveryCode = null) =>
        browser.PostAsync("/api/account/login", new
        {
            identifier = _account.Username,
            password = AccountSeed.StrongPassword,
            twoFactorCode,
            recoveryCode,
        });
}
