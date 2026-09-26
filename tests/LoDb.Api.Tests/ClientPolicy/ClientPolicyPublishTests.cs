using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Api.Tests.ClientPolicy.Units;
using LoDb.Testing;

namespace LoDb.Api.Tests.ClientPolicy;

/// <summary>
/// <c>PUT /api/admin/client-policy/{platform}</c>: an administrator publishes the policy of
/// one app, which <c>GET /api/client-policy</c> answers at once, replacing the previous one
/// whole; anyone else is refused.
/// </summary>
public sealed class ClientPolicyPublishTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string AdminPath = "/api/admin/client-policy/";
    private const string PolicyPath = "/api/client-policy";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task UnpublishedPolicyListsEveryAppWithoutVersions()
    {
        using var browser = App.Browser();

        var platforms = await PlatformsAsync(browser);

        Assert.Equal(
            ["desktop", "android"],
            platforms.Select(static p => ApiJson.Text(p, "platform")));
        Assert.All(platforms, static platform =>
        {
            Assert.Equal(JsonValueKind.Null, platform.GetProperty("minimumVersion").ValueKind);
            Assert.Equal(JsonValueKind.Null, platform.GetProperty("bundle").ValueKind);
        });
    }

    [Fact]
    public async Task PublishedPolicyIsReadBackAtOnce()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await PlatformsAsync(admin);

        using var response = await PutAsync(admin, "android", new
        {
            minimumVersion = "1.2.0",
            latestVersion = "1.4.2",
            bundle = PublishPolicyRulesTests.Bundle,
        });

        var published = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal("android", ApiJson.Text(published, "platform"));
        var android = (await PlatformsAsync(admin))[1];
        Assert.Equal(
            ("1.2.0", "1.4.2"),
            (ApiJson.Text(android, "minimumVersion"), ApiJson.Text(android, "latestVersion")));
        var bundle = android.GetProperty("bundle");
        Assert.Equal(
            (PublishPolicyRulesTests.Bundle.Id, PublishPolicyRulesTests.Checksum, "1.3.0"),
            (ApiJson.Text(bundle, "id"), ApiJson.Text(bundle, "checksum"),
                ApiJson.Text(bundle, "minimumNativeVersion")));
        Assert.Equal(JsonValueKind.String, android.GetProperty("publishedAt").ValueKind);
    }

    [Fact]
    public async Task RepublishingReplacesThePolicyWhole()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        using var first = await PutAsync(admin, "android", new
        {
            minimumVersion = "1.2.0",
            latestVersion = "1.4.2",
            bundle = PublishPolicyRulesTests.Bundle,
        });
        await ApiJson.ReadAsync(first, HttpStatusCode.OK);

        using var second = await PutAsync(admin, "android", new { latestVersion = "1.4.3" });

        await ApiJson.ReadAsync(second, HttpStatusCode.OK);
        var android = (await PlatformsAsync(admin))[1];
        Assert.Equal(JsonValueKind.Null, android.GetProperty("minimumVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, android.GetProperty("bundle").ValueKind);
        Assert.Equal("1.4.3", ApiJson.Text(android, "latestVersion"));
    }

    [Fact]
    public async Task InvalidPublicationIsRefusedFieldByField()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await PutAsync(admin, "desktop", new
        {
            minimumVersion = "2.0.0",
            latestVersion = "1.9",
            bundle = PublishPolicyRulesTests.Bundle,
        });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["invalid-version"], errors["latestVersion"]);
        Assert.Equal(["bundle-not-supported"], errors["bundle"]);
        var desktop = (await PlatformsAsync(admin))[0];
        Assert.Equal(JsonValueKind.Null, desktop.GetProperty("publishedAt").ValueKind);
    }

    [Fact]
    public async Task UnknownAppIsNotFound()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await PutAsync(admin, "ios", new { latestVersion = "1.0.0" });

        Assert.Equal(
            "unknown-platform",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task OnlyAnAdministratorPublishes()
    {
        using var member = await AdminBrowser.OpenMemberAsync(App);
        using var visitor = App.Browser();

        using var asMember = await PutAsync(member, "desktop", new { latestVersion = "1.0.0" });
        using var asVisitor = await PutAsync(visitor, "desktop", new { latestVersion = "1.0.0" });

        Assert.Equal(HttpStatusCode.Forbidden, asMember.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, asVisitor.StatusCode);
        var desktop = (await PlatformsAsync(visitor))[0];
        Assert.Equal(JsonValueKind.Null, desktop.GetProperty("latestVersion").ValueKind);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static async Task<HttpResponseMessage> PutAsync(
        BrowserClient browser,
        string platform,
        object body)
    {
        using var request = browser.Post(AdminPath + platform, body);
        request.Method = HttpMethod.Put;
        return await browser.SendAsync(request);
    }

    private static async Task<IReadOnlyList<JsonElement>> PlatformsAsync(BrowserClient browser)
    {
        using var response = await browser.GetAsync(PolicyPath);
        var policy = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        return [.. policy.GetProperty("platforms").EnumerateArray()];
    }
}
