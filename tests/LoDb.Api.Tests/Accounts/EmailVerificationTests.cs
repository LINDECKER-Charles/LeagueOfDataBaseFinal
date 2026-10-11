using System.Globalization;
using System.Net;
using System.Text;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Outbox;
using LoDb.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The verification link: it verifies the account and answers <c>alreadyVerified</c> when
/// used again within its hour; the signed-in account asks for it again five times in all,
/// then once a day.
/// </summary>
public sealed class EmailVerificationTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string VerifyPath = "/api/account/verify-email";
    private const string ResendPath = "/api/account/verify-email/resend";
    private const int FreeResends = 5;
    private const int ResendsAtOnce = 12;
    private static readonly TimeSpan ResendInterval = TimeSpan.FromDays(1);
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task LinkVerifiesTheAccountThenSaysItIsAlreadyVerified()
    {
        var account = await App.SeedAsync(new AccountSeed { EmailConfirmed = false });
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        using var resend = await browser.PostAsync(ResendPath, new { locale = "de" });
        var link = App.Outbox.LastLink(EmailTemplate.ConfirmEmail);
        using var reader = App.Browser();

        using var first = await VerifyAsync(reader, link.UserId, link.Token);
        using var second = await VerifyAsync(reader, link.UserId, link.Token);

        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.Equal(new Uri("https://localhost/de/account/verify-email"), link.Page);
        Assert.Equal(account.Id, link.UserId);
        Assert.False(await AlreadyVerifiedAsync(first));
        Assert.True(await AlreadyVerifiedAsync(second));
        Assert.True((await App.FindAsync(AccountSeed.Name)).EmailConfirmed);
        var verified = Assert.Single(
            await App.AuditAsync(),
            static entry => entry.Action == AuditAction.UserEmailVerified);
        Assert.Equal(account.Id.ToString(CultureInfo.InvariantCulture), verified.TargetId);
    }

    [Fact]
    public async Task DamagedOrForeignLinkIsRefused()
    {
        var account = await App.SeedAsync(new AccountSeed { EmailConfirmed = false });
        var other = await App.SeedAsync(new AccountSeed
        {
            Username = "Autre_1",
            Email = "autre@example.test",
            EmailConfirmed = false,
        });
        var token = await App.UsersAsync(users =>
            users.GenerateEmailConfirmationTokenAsync(account));
        var encoded = Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(token));
        using var browser = App.Browser();

        using var foreign = await VerifyAsync(browser, other.Id, encoded);
        using var damaged = await VerifyAsync(browser, account.Id, "%%not-base64%%");
        using var unknown = await VerifyAsync(browser, account.Id + 100, encoded);
        using var right = await VerifyAsync(browser, account.Id, encoded);

        foreach (var refused in new[] { foreign, damaged, unknown })
        {
            Assert.Equal(
                "invalid-token",
                await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.BadRequest));
        }

        Assert.False(await AlreadyVerifiedAsync(right));
        Assert.False((await App.FindAsync("Autre_1")).EmailConfirmed);
    }

    [Fact]
    public async Task ResendIsLimitedToFiveInAllThenOneADay()
    {
        await App.SeedAsync(new AccountSeed { EmailConfirmed = false });
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name, rememberMe: true);
        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < FreeResends; attempt++)
        {
            using var sent = await browser.PostAsync(ResendPath);
            statuses.Add(sent.StatusCode);
            App.Clock.Advance(Minute);
        }

        // The fifth went a minute ago: the sixth waits a day after it.
        using var sixth = await browser.PostAsync(ResendPath);
        App.Clock.Advance(ResendInterval - (2 * Minute));
        using var early = await browser.PostAsync(ResendPath);
        App.Clock.Advance(Minute);
        using var nextDay = await browser.PostAsync(ResendPath);
        using var sameDay = await browser.PostAsync(ResendPath);

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.Accepted, status));
        Assert.Equal(
            "resend-throttled",
            await ApiJson.ProblemCodeAsync(sixth, HttpStatusCode.TooManyRequests));
        Assert.Equal(ResendInterval - Minute, sixth.Headers.RetryAfter?.Delta);
        Assert.Equal(Minute, early.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.Accepted, nextDay.StatusCode);
        Assert.Equal(ResendInterval, sameDay.Headers.RetryAfter?.Delta);
        Assert.Equal(FreeResends + 1, App.Outbox.Sent(EmailTemplate.ConfirmEmail).Count);
        Assert.All(
            App.Outbox.Messages,
            static message => Assert.Equal(UiLocale.En, message.Locale));
    }

    [Fact]
    public async Task ResendsAskedTogetherAreCountedOneByOne()
    {
        await App.SeedAsync(new AccountSeed { EmailConfirmed = false });
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        using var first = await browser.PostAsync(ResendPath);

        var together = await Task.WhenAll(
            Enumerable.Range(0, ResendsAtOnce).Select(_ => browser.PostAsync(ResendPath)));
        var statuses = together.Select(static response => response.StatusCode).ToList();
        foreach (var response in together)
        {
            response.Dispose();
        }

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(
            FreeResends - 1,
            statuses.Count(static status => status == HttpStatusCode.Accepted));
        Assert.Equal(
            ResendsAtOnce - (FreeResends - 1),
            statuses.Count(static status => status == HttpStatusCode.TooManyRequests));
        Assert.Equal(FreeResends, App.Outbox.Sent(EmailTemplate.ConfirmEmail).Count);
    }

    [Fact]
    public async Task ResendNeedsASignedInAccountStillToVerify()
    {
        await App.SeedAsync(new AccountSeed());
        using var anonymous = App.Browser();
        using var verified = App.Browser();
        using var signIn = await verified.SignInAsync(AccountSeed.Name);

        using var withoutSession = await anonymous.PostAsync(ResendPath);
        using var alreadyVerified = await verified.PostAsync(ResendPath);

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(withoutSession, HttpStatusCode.Unauthorized));
        Assert.Equal(
            "email-already-verified",
            await ApiJson.ProblemCodeAsync(alreadyVerified, HttpStatusCode.Conflict));
        Assert.Empty(App.Outbox.Messages);
    }

    [Fact]
    public async Task BannedAccountAsksInVainBeforeItsSessionIsRevalidated()
    {
        await App.SeedAsync(new AccountSeed { EmailConfirmed = false });
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        await App.BanAsync(AccountSeed.Name);

        using var resend = await browser.PostAsync(ResendPath);

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(resend, HttpStatusCode.Unauthorized));
        Assert.Empty(App.Outbox.Messages);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static Task<HttpResponseMessage> VerifyAsync(
        BrowserClient browser,
        int userId,
        string token) =>
        browser.PostAsync(VerifyPath, new { userId, token });

    private static async Task<bool> AlreadyVerifiedAsync(HttpResponseMessage response) =>
        (await ApiJson.ReadAsync(response, HttpStatusCode.OK))
            .GetProperty("alreadyVerified").GetBoolean();
}
