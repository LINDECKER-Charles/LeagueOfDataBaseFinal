using System.Net;
using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The limits of the sign-in, at their production values: ten sign-ins a window from one
/// address, Google's included, and four failures a window for one identifier from one
/// address, refused alike whether an account bears it, before any hash and before the
/// lockout. Every attempt is counted by result, and every lockout.
/// </summary>
public sealed class SignInLimitTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string WrongPassword = "Wr0ng-passphrase!";
    private const string Client = "203.0.113.7";
    private const string OtherClient = "203.0.113.8";
    private const string Unknown = "nobody@example.test";
    private const string RateLimited = "rate-limited";
    private const int SignInsPerWindow = 10;
    private const int FailuresPerPair = 4;
    private const string AttemptsCounter = "lodb.sign_in.attempts";
    private const string WaitHistogram = "lodb.hashing.wait";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task EleventhSignInFromOneAddressIsRefused()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser().From(Client);
        var statuses = new List<HttpStatusCode>();

        for (var signIn = 0; signIn < SignInsPerWindow; signIn++)
        {
            using var allowed = await browser.SignInAsync(AccountSeed.Name);
            statuses.Add(allowed.StatusCode);
        }

        using var refused = await browser.SignInAsync(AccountSeed.Name);
        using var other = App.Browser().From(OtherClient);
        using var elsewhere = await other.SignInAsync(AccountSeed.Name);

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(
            RateLimited,
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.TooManyRequests));
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(HttpStatusCode.OK, elsewhere.StatusCode);
    }

    [Fact]
    public async Task GoogleExchangeOfTheAppsSharesTheLimit()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser().From(Client);
        for (var signIn = 0; signIn < SignInsPerWindow; signIn++)
        {
            using var allowed = await browser.SignInAsync(AccountSeed.Name);
        }

        using var exchange = browser.Post("/api/account/google/app/exchange", new
        {
            code = FakeGoogle.Code,
            codeVerifier = new string('v', 43),
            redirectUri = "com.leagueofdatabase.app:/oauth2redirect",
        });
        using var refused = await browser.SendAsync(exchange);

        Assert.Equal(
            RateLimited,
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.TooManyRequests));
        Assert.Empty(App.GoogleServer.Exchanges);
    }

    [Fact]
    public async Task FifthFailureOfAPairIsRefusedWithoutHashingNorLocking()
    {
        await App.SeedAsync(new AccountSeed());
        using var waits = App.Security<double>(WaitHistogram);
        using var browser = App.Browser().From(Client);
        var failures = await FailAsync(browser, AccountSeed.Name);

        using var throttled = await browser.SignInAsync(" LEGENDE_42 ", WrongPassword);
        using var rightButThrottled = await browser.SignInAsync(AccountSeed.Name);
        var hashed = waits.GetMeasurementSnapshot().Count;
        using var other = App.Browser().From(OtherClient);
        using var elsewhere = await other.SignInAsync(AccountSeed.Name);

        Assert.All(failures, static status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(
            RateLimited,
            await ApiJson.ProblemCodeAsync(throttled, HttpStatusCode.TooManyRequests));
        Assert.Equal(
            RateLimited,
            await ApiJson.ProblemCodeAsync(rightButThrottled, HttpStatusCode.TooManyRequests));
        Assert.Equal(FailuresPerPair, hashed);
        Assert.Equal(HttpStatusCode.OK, elsewhere.StatusCode);
    }

    [Fact]
    public async Task UnknownIdentifierIsRefusedAlikeAndCounted()
    {
        await App.SeedAsync(new AccountSeed());
        using var attempts = App.Security<long>(AttemptsCounter);
        using var browser = App.Browser().From(Client);
        await FailAsync(browser, Unknown);
        await FailAsync(browser, AccountSeed.Name);

        using var unknown = await browser.SignInAsync(Unknown, WrongPassword);
        using var known = await browser.SignInAsync(AccountSeed.Name, WrongPassword);

        Assert.Equal(
            RateLimited,
            await ApiJson.ProblemCodeAsync(unknown, HttpStatusCode.TooManyRequests));
        Assert.Equal(
            RateLimited,
            await ApiJson.ProblemCodeAsync(known, HttpStatusCode.TooManyRequests));
        Assert.Equal(unknown.Headers.RetryAfter, known.Headers.RetryAfter);
        Assert.Equal(
            [
                (SecurityMetrics.Failure, SecurityMetrics.NoAccount, FailuresPerPair),
                (SecurityMetrics.Failure, SecurityMetrics.MemberAccount, FailuresPerPair),
                (SecurityMetrics.Throttled, SecurityMetrics.UncheckedAccount, 2),
            ],
            Summary(attempts));
    }

    [Fact]
    public async Task LockoutNeedsSeveralAddressesAndIsCounted()
    {
        await App.SeedAsync(new AccountSeed());
        using var lockouts = App.Security<long>("lodb.lockouts");
        using var attempts = App.Security<long>(AttemptsCounter);

        for (var address = 1; address <= FailuresPerPair + 1; address++)
        {
            using var browser = App.Browser().From($"198.51.100.{address}");
            using var failed = await browser.SignInAsync(AccountSeed.Name, WrongPassword);
        }

        var lockout = Assert.Single(lockouts.GetMeasurementSnapshot());
        Assert.Equal("false", lockout.Tags[SecurityMetrics.IsAdminTag]);
        Assert.Equal(
            [
                (SecurityMetrics.Failure, SecurityMetrics.MemberAccount, FailuresPerPair),
                (SecurityMetrics.LockedOut, SecurityMetrics.MemberAccount, 1),
            ],
            Summary(attempts));
        Assert.All(
            attempts.GetMeasurementSnapshot(),
            static attempt => Assert.Equal("session", attempt.Tags[SecurityMetrics.ChannelTag]));
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    // As many wrong passwords as a pair may fail.
    private static async Task<List<HttpStatusCode>> FailAsync(
        BrowserClient browser,
        string identifier)
    {
        var statuses = new List<HttpStatusCode>();
        for (var failure = 0; failure < FailuresPerPair; failure++)
        {
            using var failed = await browser.SignInAsync(identifier, WrongPassword);
            statuses.Add(failed.StatusCode);
        }

        return statuses;
    }

    // The attempts by result and account, in the order of their first occurrence.
    private static List<(string Result, string Account, int Count)> Summary(
        MetricCollector<long> attempts) =>
        attempts.GetMeasurementSnapshot()
            .GroupBy(static attempt => (
                Result: (string)attempt.Tags[SecurityMetrics.ResultTag]!,
                Account: (string)attempt.Tags[SecurityMetrics.AccountTag]!))
            .Select(static group => (group.Key.Result, group.Key.Account, group.Count()))
            .ToList();
}
