using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.PublicApiKeys.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.PublicApiKeys;

/// <summary>
/// The key of an account: one active at most, issued to a verified e-mail, its secret shown
/// by the answer that issues it and never again.
/// </summary>
public sealed class KeyRulesTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private KeysApp? _app;

    private KeysApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task VisitorMustSignIn()
    {
        using var browser = App.Browser();

        using var response = await browser.GetAsync(KeysApp.PortalPath);

        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task AccountWithoutKeyReadsNone()
    {
        using var browser = await App.SignedInAsync(await App.SeedUserAsync("SansCle"));

        using var response = await browser.GetAsync(KeysApp.PortalPath);

        var state = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, state.GetProperty("key").ValueKind);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task SecretIsShownOnceAndOnlyItsFingerprintIsStored()
    {
        var owner = await App.SeedUserAsync("Createur");
        using var browser = await App.SignedInAsync(owner);

        using var created = await browser.PostAsync(KeysApp.PortalPath, new { name = " my-app " });
        using var read = await browser.GetAsync(KeysApp.PortalPath);

        var issued = await ApiJson.ReadAsync(created, HttpStatusCode.OK);
        var secret = ApiJson.Text(issued, "secret")!;
        Assert.Matches("^lodb_[0-9a-f]{40}$", secret);
        var key = Assert.Single(await App.KeysOfAsync(owner.Id));
        Assert.Equal(Fingerprint(secret), key.KeyHash);
        Assert.Equal((secret[..12], "my-app", "free"), (key.KeyPrefix, key.Name, key.Plan));
        Assert.Equal((500, 10, 0L), (key.MonthlyQuota, key.RateLimitPerMin, key.CreditsBalance));
        var body = await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
        var overview = JsonDocument.Parse(body).RootElement.GetProperty("key");
        Assert.Equal(secret[..12], ApiJson.Text(overview, "prefix"));
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task CreationIsAuditedAndReportedToV1()
    {
        var owner = await App.SeedUserAsync("Journal");
        using var browser = await App.SignedInAsync(owner);

        using var created = await browser.PostAsync(KeysApp.PortalPath, new { });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var key = Assert.Single(await App.KeysOfAsync(owner.Id));
        Assert.Equal("default", key.Name);
        Assert.Equal([key.Id], App.KeyCache.Invalidated);
        var entry = Assert.Single(
            await App.AuditAsync(),
            static entry => entry.Action == AuditAction.ApiKeyCreate);
        Assert.Equal((AuditActorType.User, (int?)owner.Id), (entry.ActorType, entry.ActorId));
        Assert.Equal((AuditTargetType.ApiKey, key.KeyPrefix + "…"), (entry.TargetType, entry.Target));
    }

    [Fact]
    public async Task UnverifiedEmailCannotCreateAKey()
    {
        var owner = await App.SeedUserAsync("NonVerifie", verified: false);
        using var browser = await App.SignedInAsync(owner);

        using var response = await browser.PostAsync(KeysApp.PortalPath, new { name = "x" });

        Assert.Equal(
            "email-not-verified",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Empty(await App.KeysOfAsync(owner.Id));
    }

    [Fact]
    public async Task ASecondActiveKeyIsRefused()
    {
        var owner = await App.SeedUserAsync("Double");
        using var browser = await App.SignedInAsync(owner);
        using var first = await browser.PostAsync(KeysApp.PortalPath, new { name = "one" });

        using var second = await browser.PostAsync(KeysApp.PortalPath, new { name = "two" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(
            "api-key-exists",
            await ApiJson.ProblemCodeAsync(second, HttpStatusCode.Conflict));
        Assert.Equal("one", Assert.Single(await App.KeysOfAsync(owner.Id)).Name);
    }

    [Fact]
    public async Task ConcurrentCreationsIssueOneKey()
    {
        var owner = await App.SeedUserAsync("Presse");
        using var browser = await App.SignedInAsync(owner);

        var answers = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => browser.PostAsync(KeysApp.PortalPath, new { name = "race" })));

        Assert.Single(answers, static answer => answer.StatusCode == HttpStatusCode.OK);
        Assert.Equal(3, answers.Count(static a => a.StatusCode == HttpStatusCode.Conflict));
        Assert.Single(await App.KeysOfAsync(owner.Id));
        foreach (var answer in answers)
        {
            answer.Dispose();
        }
    }

    [Fact]
    public async Task ACallFromAnotherPageIsRefused()
    {
        var owner = await App.SeedUserAsync("Forge");
        using var browser = await App.SignedInAsync(owner);
        using var request = browser.Post(KeysApp.PortalPath, new { name = "x" });
        request.Headers.Remove(BrowserClient.XsrfHeader);

        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await App.KeysOfAsync(owner.Id));
    }

    public async ValueTask InitializeAsync() => _app = await KeysApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static string Fingerprint(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
