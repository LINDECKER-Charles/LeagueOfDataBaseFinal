using System.Globalization;
using System.Net;
using System.Text;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Outbox;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The verification link: it verifies the account and answers <c>alreadyVerified</c> when
/// used again within its hour; the signed-in account asks for it again five times in all.
/// The pace beyond is <see cref="OwnHost.VerificationResendLimitTests"/>'.
/// </summary>
/// <remarks>The tests share a host, each with accounts of its own.</remarks>
public sealed class EmailVerificationTests(SharedAccountsApp host)
    : IClassFixture<SharedAccountsApp>
{
    private const string VerifyPath = "/api/account/verify-email";
    private const string ResendPath = "/api/account/verify-email/resend";
    private const int FreeResends = 5;
    private const int ResendsAtOnce = 12;

    private readonly AccountSeed _account = host.Unique(new AccountSeed { EmailConfirmed = false });
    private readonly AccountSeed _other = host.Unique(new AccountSeed { EmailConfirmed = false });

    private AccountsApp App { get; } = host.App;

    [Fact]
    public async Task LinkVerifiesTheAccountThenSaysItIsAlreadyVerified()
    {
        var account = await App.SeedAsync(_account);
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(_account.Username);
        using var resend = await browser.PostAsync(ResendPath, new { locale = "de" });
        var link = App.Outbox.LastLink(_account.Email, EmailTemplate.ConfirmEmail);
        using var reader = App.Browser();

        using var first = await VerifyAsync(reader, link.UserId, link.Token);
        using var second = await VerifyAsync(reader, link.UserId, link.Token);

        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.Equal(new Uri("https://localhost/de/account/verify-email"), link.Page);
        Assert.Equal(account.Id, link.UserId);
        Assert.False(await AlreadyVerifiedAsync(first));
        Assert.True(await AlreadyVerifiedAsync(second));
        Assert.True((await App.FindAsync(_account.Username)).EmailConfirmed);
        var verified = Assert.Single(
            await App.AuditOfAsync(_account.Username),
            static entry => entry.Action == AuditAction.UserEmailVerified);
        Assert.Equal(account.Id.ToString(CultureInfo.InvariantCulture), verified.TargetId);
    }

    [Fact]
    public async Task DamagedOrForeignLinkIsRefused()
    {
        var account = await App.SeedAsync(_account);
        var other = await App.SeedAsync(_other);
        var token = await App.UsersAsync(users =>
            users.GenerateEmailConfirmationTokenAsync(account));
        var encoded = Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(token));
        using var browser = App.Browser();

        using var foreign = await VerifyAsync(browser, other.Id, encoded);
        using var damaged = await VerifyAsync(browser, account.Id, "%%not-base64%%");
        using var unknown = await VerifyAsync(browser, other.Id + 100, encoded);
        using var right = await VerifyAsync(browser, account.Id, encoded);

        foreach (var refused in new[] { foreign, damaged, unknown })
        {
            Assert.Equal(
                "invalid-token",
                await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.BadRequest));
        }

        Assert.False(await AlreadyVerifiedAsync(right));
        Assert.False((await App.FindAsync(_other.Username)).EmailConfirmed);
    }

    [Fact]
    public async Task ResendsAskedTogetherAreCountedOneByOne()
    {
        await App.SeedAsync(_account);
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(_account.Username);
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
        Assert.Equal(FreeResends, App.Outbox.To(_account.Email).Count);
    }

    [Fact]
    public async Task ResendNeedsASignedInAccountStillToVerify()
    {
        await App.SeedAsync(_account with { EmailConfirmed = true });
        using var anonymous = App.Browser();
        using var verified = App.Browser();
        using var signIn = await verified.SignInAsync(_account.Username);

        using var withoutSession = await anonymous.PostAsync(ResendPath);
        using var alreadyVerified = await verified.PostAsync(ResendPath);

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(withoutSession, HttpStatusCode.Unauthorized));
        Assert.Equal(
            "email-already-verified",
            await ApiJson.ProblemCodeAsync(alreadyVerified, HttpStatusCode.Conflict));
        Assert.Empty(App.Outbox.To(_account.Email));
    }

    [Fact]
    public async Task BannedAccountAsksInVainBeforeItsSessionIsRevalidated()
    {
        await App.SeedAsync(_account);
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(_account.Username);
        await App.BanAsync(_account.Username);

        using var resend = await browser.PostAsync(ResendPath);

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(resend, HttpStatusCode.Unauthorized));
        Assert.Empty(App.Outbox.To(_account.Email));
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
