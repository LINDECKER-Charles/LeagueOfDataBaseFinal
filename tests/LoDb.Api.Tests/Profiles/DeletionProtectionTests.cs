using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Profiles.Support;

namespace LoDb.Api.Tests.Profiles;

/// <summary>
/// The erasure guards its confirmation as the sign-in guards a password: a wrong password
/// counts toward the lockout, and an account confirms five times a window at most, whatever
/// the address it calls from.
/// </summary>
[Collection(ProfilesGroup.Name)]
public sealed class DeletionProtectionTests(ProfilesApp app)
{
    private const string WrongPassword = "faux";
    private const int FailuresToLock = 5;
    private const int ConfirmationsPerWindow = 5;
    private const string AccountLocked = "account-locked";
    private const string RateLimited = "rate-limited";

    [Fact]
    public async Task FifthWrongPasswordLocksTheAccount()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);
        var statuses = new List<HttpStatusCode>();

        for (var failure = 1; failure < FailuresToLock; failure++)
        {
            using var wrong = await browser.PostAsync(
                ProfileCalls.Delete,
                new { password = WrongPassword });
            statuses.Add(wrong.StatusCode);
        }

        using var locking = await browser.PostAsync(
            ProfileCalls.Delete,
            new { password = WrongPassword });
        using var stranger = app.Browser();
        using var signIn = await stranger.SignInAsync(account.Username);

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.BadRequest, status));
        Assert.Equal(
            AccountLocked,
            await ApiJson.ProblemCodeAsync(locking, HttpStatusCode.TooManyRequests));
        Assert.True(locking.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(
            AccountLocked,
            await ApiJson.ProblemCodeAsync(signIn, HttpStatusCode.TooManyRequests));
        Assert.NotNull(await app.FindAsync(account.Username));
    }

    [Fact]
    public async Task SixthConfirmationOfAnAccountIsRefusedFromAnyAddress()
    {
        var account = await app.SeedAsync(new AccountSeed { GoogleId = GoogleId() });
        var other = await app.SeedAsync(new AccountSeed { GoogleId = GoogleId() });
        using var browser = await app.SignInAsync(account);
        using var otherBrowser = await app.SignInAsync(other);
        var statuses = new List<HttpStatusCode>();

        for (var confirmation = 0; confirmation < ConfirmationsPerWindow; confirmation++)
        {
            browser.From($"198.51.100.{confirmation + 1}");
            using var wrong = await browser.PostAsync(ProfileCalls.Delete, WrongPhrase());
            statuses.Add(wrong.StatusCode);
        }

        browser.From("198.51.100.200");
        using var refused = await browser.PostAsync(ProfileCalls.Delete, WrongPhrase());
        using var elsewhere = await otherBrowser.PostAsync(ProfileCalls.Delete, WrongPhrase());

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.BadRequest, status));
        Assert.Equal(
            RateLimited,
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.BadRequest, elsewhere.StatusCode);
    }

    private static object WrongPhrase() =>
        new { confirmation = "garder mon compte", locale = "fr" };

    // Google subjects are 21 digits; the column holds 30.
    private static string GoogleId() => Guid.NewGuid().ToString("N")[..21];
}
