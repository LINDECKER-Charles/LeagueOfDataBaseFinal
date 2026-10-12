using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.PublicApiKeys.Support;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Tests.PublicApiKeys;

/// <summary>
/// A revoked key is refused by <c>/v1</c> from the next request, where the legacy stack
/// waited up to a minute of go-api's cache; the account may then issue another.
/// </summary>
/// <remarks>The tests share a host, each with accounts of its own.</remarks>
public sealed class RevocationTests(SharedKeysApp host) : IClassFixture<SharedKeysApp>
{
    private KeysApp App { get; } = host.App;

    [Fact]
    public async Task ARevokedKeyIsRefusedOnV1AtOnce()
    {
        var owner = await App.SeedUserAsync("Revoque");
        using var browser = await App.SignedInAsync(owner);
        var secret = await IssueAsync(browser);
        using var before = await App.AskV1Async(secret);

        using var revoked = await browser.SendAsync(Delete(browser));
        using var after = await App.AskV1Async(secret);

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        using var body = JsonDocument.Parse(
            await after.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "forbidden",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task RevocationIsStoredAuditedAndFreesTheAccount()
    {
        var owner = await App.SeedUserAsync("Libere");
        using var browser = await App.SignedInAsync(owner);
        await IssueAsync(browser);

        using var revoked = await browser.SendAsync(Delete(browser));
        using var read = await browser.GetAsync(KeysApp.PortalPath);
        var again = await IssueAsync(browser);

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        var state = await ApiJson.ReadAsync(read, HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, state.GetProperty("key").ValueKind);
        var keys = await App.KeysOfAsync(owner.Id);
        Assert.Equal([false, true], keys.Select(static key => key.IsActive));
        Assert.NotNull(keys[0].RevokedAt);
        Assert.Equal(again[..12], keys[1].KeyPrefix);
        var entry = Assert.Single(
            await App.AuditOfAsync(owner.Id),
            static entry => entry.Action == AuditAction.ApiKeyRevoke);
        Assert.Equal(keys[0].KeyPrefix + "…", entry.Target);
    }

    [Fact]
    public async Task NothingToRevokeWithoutAKey()
    {
        using var browser = await App.SignedInAsync(await App.SeedUserAsync("SansCle"));

        using var response = await browser.SendAsync(Delete(browser));

        Assert.Equal(
            "api-key-missing",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.NotFound));
    }

    // A DELETE as the front sends it: the page's origin and its XSRF token.
    private static HttpRequestMessage Delete(BrowserClient browser)
    {
        var request = browser.Post(KeysApp.PortalPath);
        request.Method = HttpMethod.Delete;
        return request;
    }

    private static async Task<string> IssueAsync(BrowserClient browser)
    {
        using var response = await browser.PostAsync(KeysApp.PortalPath, new { name = "prod" });
        return ApiJson.Text(await ApiJson.ReadAsync(response, HttpStatusCode.OK), "secret")!;
    }
}
