using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Telemetry;

/// <summary>
/// The <c>LoDb.Security</c> meter reaches the scrape under the names of the metrics contract,
/// with closed label values only: never an identifier, an address or a name.
/// </summary>
public sealed class SecurityMetricsScrapeTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string WrongPassword = "Wr0ng-passphrase!";
    private static readonly Uri Metrics = new("/metrics", UriKind.Relative);

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task ASignInIsScrapedUnderTheContractNames()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var failed = await browser.SignInAsync(AccountSeed.Name, WrongPassword);
        using var scraper = App.Scraper();

        using var response = await scraper.GetAsync(
            Metrics,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = (await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken)).Split('\n');
        Assert.Contains(lines, static line => line.StartsWith(
            "lodb_hashing_in_flight{",
            StringComparison.Ordinal));
        Assert.Contains(lines, static line => line.StartsWith(
            "lodb_hashing_wait_seconds_count{",
            StringComparison.Ordinal));
        // The exporter listens to every meter of the name in the process, the other test hosts'
        // included: other series may show up, never with an identifier.
        var attempts = lines
            .Where(static line => line.StartsWith(
                "lodb_sign_in_attempts_total{",
                StringComparison.Ordinal))
            .ToList();
        Assert.Contains(attempts, static attempt =>
            attempt.Contains("result=\"failure\"", StringComparison.Ordinal)
            && attempt.Contains("account=\"member\"", StringComparison.Ordinal)
            && attempt.Contains("channel=\"session\"", StringComparison.Ordinal));
        Assert.All(attempts, static attempt => Assert.DoesNotContain(
            AccountSeed.Name,
            attempt,
            StringComparison.OrdinalIgnoreCase));
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
