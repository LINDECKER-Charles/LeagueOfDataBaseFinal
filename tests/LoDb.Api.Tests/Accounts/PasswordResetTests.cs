using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Outbox;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The forgotten password: the same answer whether an account uses the e-mail or not, one
/// e-mail an hour per account and five requests an hour per address, a link its page checks
/// without using it, and that sets the new password once, unlocks the account and closes its
/// sessions.
/// </summary>
public sealed class PasswordResetTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string ForgotPath = "/api/account/forgot-password";
    private const string ResetPath = "/api/account/reset-password";
    private const string CheckPath = "/api/account/reset-password/check";
    private const string NewPassword = "N0uvelle-phrase!";
    private const string WrongPassword = "Wr0ng-passphrase!";
    private const int RequestsPerHour = 5;
    private const int FailuresToLock = 5;
    private const int ChecksPerHour = 30;

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task AnswerIsTheSameWhetherAnAccountUsesTheEmailOrNot()
    {
        await App.SeedAsync(new AccountSeed());
        await App.SeedAsync(new AccountSeed
        {
            Username = "Banni_1",
            Email = "banni@example.test",
            Banned = true,
        });
        using var browser = App.Browser();

        using var known = await ForgotAsync(browser, " Legende@Example.TEST ");
        using var unknown = await ForgotAsync(browser, "nobody@example.test");
        using var banned = await ForgotAsync(browser, "banni@example.test");
        using var malformed = await ForgotAsync(browser, "legende");

        Assert.Equal(
            [HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.Accepted],
            [known.StatusCode, unknown.StatusCode, banned.StatusCode]);
        var mail = Assert.Single(App.Outbox.Messages);
        Assert.Equal(
            (AccountSeed.Address, EmailTemplate.ResetPassword),
            (mail.Recipient, mail.Template));
        var link = EmailLink.Of(mail);
        Assert.Equal(new Uri("https://localhost/en/account/reset-password"), link.Page);
        Assert.Equal(
            $"https://localhost/en/account/reset-password/{link.Token}?user={link.UserId}",
            mail.Model[EmailModelKeys.ActionUrl]);
        var errors = await ApiJson.FieldErrorsAsync(malformed);
        Assert.Equal(["email-invalid"], errors["email"]);
    }

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
    public async Task LinkSetsTheNewPasswordOnceAndUnlocksTheAccount()
    {
        var account = await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        await LockAsync(browser);
        using var forgot = await ForgotAsync(browser, AccountSeed.Address);
        var link = App.Outbox.LastLink(EmailTemplate.ResetPassword);

        using var reset = await ResetAsync(browser, link, NewPassword);
        using var reused = await ResetAsync(browser, link, "Tr0isieme-phrase!");
        using var oldPassword = await browser.SignInAsync(AccountSeed.Name);
        using var newPassword = await browser.SignInAsync(AccountSeed.Name, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(
            "invalid-token",
            await ApiJson.ProblemCodeAsync(reused, HttpStatusCode.BadRequest));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
        var entry = Assert.Single(
            await App.AuditAsync(),
            static entry => entry.Action == AuditAction.UserPasswordReset);
        Assert.Equal(AccountSeed.Name, entry.Target);
        Assert.Equal(account.Id.ToString(CultureInfo.InvariantCulture), entry.TargetId);
    }

    [Fact]
    public async Task ResetClosesTheOpenSessions()
    {
        await App.SeedAsync(new AccountSeed());
        using var session = App.Browser();
        using var signIn = await session.SignInAsync(AccountSeed.Name, rememberMe: true);
        using var browser = App.Browser();
        using var forgot = await ForgotAsync(browser, AccountSeed.Address);

        using var reset = await ResetAsync(
            browser,
            App.Outbox.LastLink(EmailTemplate.ResetPassword),
            NewPassword);
        App.Clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Null(await session.SignedInAsAsync());
    }

    [Fact]
    public async Task WeakPasswordIsRefusedAndTheLinkStaysUsable()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var forgot = await ForgotAsync(browser, AccountSeed.Address);
        var link = App.Outbox.LastLink(EmailTemplate.ResetPassword);

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
    public async Task CheckTellsAUsableLinkWithoutUsingIt()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var forgot = await ForgotAsync(browser, AccountSeed.Address);
        var link = App.Outbox.LastLink(EmailTemplate.ResetPassword);

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
        var other = await App.SeedAsync(new AccountSeed
        {
            Username = "Autre_1",
            Email = "autre@example.test",
        });
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var forgot = await ForgotAsync(browser, AccountSeed.Address);
        var link = App.Outbox.LastLink(EmailTemplate.ResetPassword);

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

    [Fact]
    public async Task CheckHasItsOwnHourlyLimitApartFromTheRequests()
    {
        using var browser = App.Browser();
        var damaged = new EmailLink(AccountsApp.BaseAddress, UserId: 1, Token: "bm9wZQ");
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt <= ChecksPerHour; attempt++)
        {
            using var response = await CheckAsync(browser, damaged);
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

    private static Task<HttpResponseMessage> ResetAsync(
        BrowserClient browser,
        EmailLink link,
        string password) =>
        browser.PostAsync(ResetPath, new { userId = link.UserId, token = link.Token, password });

    private static Task<HttpResponseMessage> CheckAsync(BrowserClient browser, EmailLink link) =>
        browser.PostAsync(CheckPath, new { userId = link.UserId, token = link.Token });

    private static async Task LockAsync(BrowserClient browser)
    {
        for (var failure = 0; failure < FailuresToLock; failure++)
        {
            using var attempt = await browser.SignInAsync(AccountSeed.Name, WrongPassword);
        }

        using var locked = await browser.SignInAsync(AccountSeed.Name);
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }
}
