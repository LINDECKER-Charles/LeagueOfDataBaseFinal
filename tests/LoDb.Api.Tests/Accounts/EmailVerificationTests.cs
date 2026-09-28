using System.Globalization;
using System.Net;
using System.Text;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Outbox;
using LoDb.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The verification link: it verifies the account and answers <c>alreadyVerified</c> when
/// used again within its hour; the signed-in account asks for it three times per quarter of
/// an hour at most.
/// </summary>
public sealed class EmailVerificationTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string VerifyPath = "/api/account/verify-email";
    private const string ResendPath = "/api/account/verify-email/resend";
    private const int ResendsPerWindow = 3;
    private static readonly TimeSpan ResendWindow = TimeSpan.FromMinutes(15);

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
    public async Task ResendIsLimitedToThreeEveryQuarterOfAnHour()
    {
        await App.SeedAsync(new AccountSeed { EmailConfirmed = false });
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < ResendsPerWindow; attempt++)
        {
            using var sent = await browser.PostAsync(ResendPath);
            statuses.Add(sent.StatusCode);
            App.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        using var throttled = await browser.PostAsync(ResendPath);
        var retryAfter = throttled.Headers.RetryAfter?.Delta;
        App.Clock.Advance(ResendWindow - TimeSpan.FromMinutes(ResendsPerWindow));
        using var again = await browser.PostAsync(ResendPath);

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.Accepted, status));
        Assert.Equal(
            "resend-throttled",
            await ApiJson.ProblemCodeAsync(throttled, HttpStatusCode.TooManyRequests));
        Assert.Equal(ResendWindow - TimeSpan.FromMinutes(ResendsPerWindow), retryAfter);
        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        Assert.Equal(ResendsPerWindow + 1, App.Outbox.Sent(EmailTemplate.ConfirmEmail).Count);
        Assert.All(
            App.Outbox.Messages,
            static message => Assert.Equal(UiLocale.En, message.Locale));
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

    [Fact]
    public void LinksLastOneHour()
    {
        var options = App.Services
            .GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;

        Assert.Equal(TimeSpan.FromHours(1), options.TokenLifespan);
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
