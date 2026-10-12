using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Outbox;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The forgotten password: the same answer whether an account uses the e-mail or not, a link
/// its page checks without using it, and that sets the new password once and unlocks the
/// account. Its limits and the sessions it closes are
/// <see cref="OwnHost.PasswordResetLimitTests"/>'.
/// </summary>
/// <remarks>
/// The tests share a host: each asks from an address and for accounts of its own.
/// </remarks>
public sealed class PasswordResetTests(SharedAccountsApp host)
    : IClassFixture<SharedAccountsApp>
{
    private const string ForgotPath = "/api/account/forgot-password";
    private const string ResetPath = "/api/account/reset-password";
    private const string CheckPath = "/api/account/reset-password/check";
    private const string NewPassword = "N0uvelle-phrase!";
    private const string WrongPassword = "Wr0ng-passphrase!";
    private const string Unknown = "nobody@example.test";
    private const int FailuresToLock = 5;

    private readonly AccountSeed _account = host.Unique();
    private readonly AccountSeed _other = host.Unique();
    private readonly string _address = host.NextAddress();

    private AccountsApp App { get; } = host.App;

    [Fact]
    public async Task AnswerIsTheSameWhetherAnAccountUsesTheEmailOrNot()
    {
        await App.SeedAsync(_account);
        await App.SeedAsync(_other with { Banned = true });
        using var browser = Browser();

        using var known = await ForgotAsync(browser, $" {_account.Email.ToUpperInvariant()} ");
        using var unknown = await ForgotAsync(browser, Unknown);
        using var banned = await ForgotAsync(browser, _other.Email);
        using var malformed = await ForgotAsync(browser, "legende");

        Assert.Equal(
            [HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.Accepted],
            [known.StatusCode, unknown.StatusCode, banned.StatusCode]);
        var mail = Assert.Single(App.Outbox.To(_account.Email));
        Assert.Equal(EmailTemplate.ResetPassword, mail.Template);
        Assert.Empty(App.Outbox.To(_other.Email));
        Assert.Empty(App.Outbox.To(Unknown));
        var link = EmailLink.Of(mail);
        Assert.Equal(new Uri("https://localhost/en/account/reset-password"), link.Page);
        Assert.Equal(
            $"https://localhost/en/account/reset-password?user={link.UserId}#token={link.Token}",
            mail.Model[EmailModelKeys.ActionUrl]);
        var errors = await ApiJson.FieldErrorsAsync(malformed);
        Assert.Equal(["email-invalid"], errors["email"]);
    }

    [Fact]
    public async Task LinkSetsTheNewPasswordOnceAndUnlocksTheAccount()
    {
        var account = await App.SeedAsync(_account);
        using var browser = Browser();
        await LockAsync();
        using var forgot = await ForgotAsync(browser, _account.Email);
        var link = App.Outbox.LastLink(_account.Email, EmailTemplate.ResetPassword);

        using var reset = await ResetAsync(browser, link, NewPassword);
        using var reused = await ResetAsync(browser, link, "Tr0isieme-phrase!");
        using var oldPassword = await browser.SignInAsync(_account.Username);
        using var newPassword = await browser.SignInAsync(_account.Username, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(
            "invalid-token",
            await ApiJson.ProblemCodeAsync(reused, HttpStatusCode.BadRequest));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
        var entry = Assert.Single(
            await App.AuditOfAsync(_account.Username),
            static entry => entry.Action == AuditAction.UserPasswordReset);
        Assert.Equal(_account.Username, entry.Target);
        Assert.Equal(account.Id.ToString(CultureInfo.InvariantCulture), entry.TargetId);
    }

    [Fact]
    public async Task WeakPasswordIsRefusedAndTheLinkStaysUsable()
    {
        await App.SeedAsync(_account);
        using var browser = Browser();
        using var forgot = await ForgotAsync(browser, _account.Email);
        var link = App.Outbox.LastLink(_account.Email, EmailTemplate.ResetPassword);

        using var weak = await ResetAsync(browser, link, "password");
        using var damaged = await ResetAsync(browser, link with { Token = "bm9wZQ" }, NewPassword);
        using var strong = await ResetAsync(browser, link, NewPassword);

        var errors = await ApiJson.FieldErrorsAsync(weak);
        Assert.Contains("auth.password.too_common", errors["password"]);
        Assert.Equal(
            "invalid-token",
            await ApiJson.ProblemCodeAsync(damaged, HttpStatusCode.BadRequest));
        Assert.Equal(HttpStatusCode.NoContent, strong.StatusCode);
    }

    [Fact]
    public async Task CheckTellsAUsableLinkWithoutUsingIt()
    {
        await App.SeedAsync(_account);
        using var browser = Browser();
        using var forgot = await ForgotAsync(browser, _account.Email);
        var link = App.Outbox.LastLink(_account.Email, EmailTemplate.ResetPassword);

        using var opened = await CheckAsync(browser, link);
        using var reopened = await CheckAsync(browser, link);
        using var reset = await ResetAsync(browser, link, NewPassword);
        using var used = await CheckAsync(browser, link);

        Assert.Equal(
            [HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.NoContent],
            [opened.StatusCode, reopened.StatusCode, reset.StatusCode]);
        Assert.Equal(
            "invalid-token",
            await ApiJson.ProblemCodeAsync(used, HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task CheckRefusesADamagedLinkOrOneOfAnotherAccount()
    {
        var other = await App.SeedAsync(_other);
        await App.SeedAsync(_account);
        using var browser = Browser();
        using var forgot = await ForgotAsync(browser, _account.Email);
        var link = App.Outbox.LastLink(_account.Email, EmailTemplate.ResetPassword);

        using var damaged = await CheckAsync(browser, link with { Token = "bm9wZQ" });
        using var empty = await CheckAsync(browser, link with { Token = "" });
        using var otherAccount = await CheckAsync(browser, link with { UserId = other.Id });

        foreach (var refused in new[] { damaged, empty, otherAccount })
        {
            Assert.Equal(
                "invalid-token",
                await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.BadRequest));
        }
    }

    private static Task<HttpResponseMessage> ForgotAsync(BrowserClient browser, string email) =>
        browser.PostAsync(ForgotPath, new { email });

    private static Task<HttpResponseMessage> ResetAsync(
        BrowserClient browser,
        EmailLink link,
        string password) =>
        browser.PostAsync(ResetPath, new { userId = link.UserId, token = link.Token, password });

    private static Task<HttpResponseMessage> CheckAsync(BrowserClient browser, EmailLink link) =>
        browser.PostAsync(CheckPath, new { userId = link.UserId, token = link.Token });

    // The requests and checks are limited by address: this test's own.
    private BrowserClient Browser() => App.Browser().From(_address);

    // From as many addresses: one address alone runs out of attempts before the lockout.
    private async Task LockAsync()
    {
        for (var failure = 0; failure < FailuresToLock; failure++)
        {
            using var stranger = App.Browser().From($"198.51.100.{failure + 1}");
            using var attempt = await stranger.SignInAsync(_account.Username, WrongPassword);
        }

        using var browser = App.Browser();
        using var locked = await browser.SignInAsync(_account.Username);
        Assert.Equal(
            "account-locked",
            await ApiJson.ProblemCodeAsync(locked, HttpStatusCode.TooManyRequests));
    }
}
