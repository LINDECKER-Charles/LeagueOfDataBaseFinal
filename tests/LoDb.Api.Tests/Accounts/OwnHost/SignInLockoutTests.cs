using System.Net;
using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts.OwnHost;

/// <summary>
/// A lock after five failed sign-ins from several addresses, even for the right password,
/// counted by the meters of the security.
/// </summary>
/// <remarks>A host of its own: the test reads the meters.</remarks>
public sealed class SignInLockoutTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string WrongPassword = "Wr0ng-passphrase!";
    private const int FailuresToLock = 5;
    private const int LockoutSeconds = 15 * 60;
    private const string AttemptsCounter = "lodb.sign_in.attempts";
    private const string LockoutsCounter = "lodb.lockouts";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task FifthFailureLocksTheAccountEvenForItsPasswordAndIsCounted()
    {
        await App.SeedAsync(new AccountSeed());
        using var lockouts = App.Security<long>(LockoutsCounter);
        using var attempts = App.Security<long>(AttemptsCounter);
        var statuses = new List<HttpStatusCode>();

        // From as many addresses: one address alone runs out of attempts before the lockout.
        for (var failure = 0; failure < FailuresToLock; failure++)
        {
            using var stranger = App.Browser().From($"198.51.100.{failure + 1}");
            using var attempt = await stranger.SignInAsync(AccountSeed.Name, WrongPassword);
            statuses.Add(attempt.StatusCode);
        }

        var counted = attempts.GetMeasurementSnapshot();
        var locked = lockouts.GetMeasurementSnapshot();
        using var browser = App.Browser();
        using var right = await browser.SignInAsync(AccountSeed.Name);

        Assert.Equal(
            [.. Enumerable.Repeat(HttpStatusCode.Unauthorized, FailuresToLock - 1),
                HttpStatusCode.TooManyRequests],
            statuses);
        var lockout = Assert.Single(locked);
        Assert.Equal("false", lockout.Tags[SecurityMetrics.IsAdminTag]);
        Assert.Equal(
            [.. Enumerable.Repeat(SecurityMetrics.Failure, FailuresToLock - 1),
                SecurityMetrics.LockedOut],
            counted.Select(static attempt => (string?)attempt.Tags[SecurityMetrics.ResultTag]));
        Assert.All(counted, static attempt => Assert.Equal(
            (SecurityMetrics.MemberAccount, "session"),
            ((string?)attempt.Tags[SecurityMetrics.AccountTag],
                (string?)attempt.Tags[SecurityMetrics.ChannelTag])));
        var retryAfter = right.Headers.RetryAfter?.Delta?.TotalSeconds;
        Assert.Equal(
            "account-locked",
            await ApiJson.ProblemCodeAsync(right, HttpStatusCode.TooManyRequests));
        Assert.InRange(retryAfter ?? 0, LockoutSeconds - 60, LockoutSeconds);
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
