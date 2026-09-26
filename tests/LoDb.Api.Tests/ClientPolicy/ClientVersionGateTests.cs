using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.ClientPolicy.Support;
using LoDb.Infrastructure.Persistence.Apps;
using LoDb.Testing;

namespace LoDb.Api.Tests.ClientPolicy;

/// <summary>
/// The version gate of ADR 0008: an app below the minimum of its policy is answered
/// <c>426 Upgrade Required</c> on every <c>/api</c> call but the policy itself; the web,
/// which sends no <c>X-LoDb-Client</c>, never is.
/// </summary>
public sealed class ClientVersionGateTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string ClientHeader = "X-LoDb-Client";
    private const string SessionPath = "/api/account/me";
    private const string PolicyPath = "/api/client-policy";

    private ClientPolicyApp? _app;

    private ClientPolicyApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("desktop/1.3.9")]
    [InlineData("desktop/1.4.0-beta.2")]
    [InlineData("desktop/0.9.0")]
    public async Task AppBelowTheMinimumIsToldToUpgrade(string client)
    {
        await App.SeedAsync(ClientPolicyApp.Policy(AppPlatform.Desktop, "1.4.0", "1.5.2"));
        using var counter = App.UpgradeRequiredCounter();
        using var http = App.Client();

        using var response = await SendAsync(http, SessionPath, client);

        var problem = await ApiJson.ReadAsync(response, HttpStatusCode.UpgradeRequired);
        Assert.Equal(ApiJson.ProblemMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(
            ("client-upgrade-required", "desktop", client["desktop/".Length..], "1.4.0", "1.5.2"),
            (ApiJson.Text(problem, "code"), ApiJson.Text(problem, "platform"),
                ApiJson.Text(problem, "clientVersion"), ApiJson.Text(problem, "minimumVersion"),
                ApiJson.Text(problem, "latestVersion")));
        Assert.Equal(426, problem.GetProperty("status").GetInt32());
        var measurement = Assert.Single(counter.GetMeasurementSnapshot());
        Assert.Equal((1, "desktop"), (measurement.Value, measurement.Tags["platform"]));
    }

    [Theory]
    [InlineData("desktop/1.4.0")]
    [InlineData("desktop/1.4.1-beta.1")]
    [InlineData("desktop/1.10.0")]
    [InlineData("android/1.0.0")]
    public async Task AppAtOrAboveItsMinimumGoesThrough(string client)
    {
        await App.SeedAsync(
            ClientPolicyApp.Policy(AppPlatform.Desktop, "1.4.0", null),
            ClientPolicyApp.Policy(AppPlatform.Android, null, "1.0.0"));
        using var http = App.Client();

        using var response = await SendAsync(http, SessionPath, client);

        await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("web/0.0.1")]
    [InlineData("desktop/old")]
    [InlineData("desktop")]
    public async Task RequestWithoutAReadableClientIsNeverConcerned(string? client)
    {
        await App.SeedAsync(ClientPolicyApp.Policy(AppPlatform.Desktop, "99.0.0", "99.0.0"));
        using var counter = App.UpgradeRequiredCounter();
        using var http = App.Client();

        using var response = await SendAsync(http, SessionPath, client);

        await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Empty(counter.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task OutdatedAppStillReadsThePolicy()
    {
        await App.SeedAsync(ClientPolicyApp.Policy(AppPlatform.Desktop, "2.0.0", "2.1.0"));
        using var http = App.Client();

        using var response = await SendAsync(http, PolicyPath, "desktop/1.0.0");

        var policy = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        var desktop = policy.GetProperty("platforms")[0];
        Assert.Equal("2.1.0", ApiJson.Text(desktop, "latestVersion"));
    }

    [Fact]
    public async Task AndroidAppCanReadItsUpgradeAnswerAcrossOrigins()
    {
        await App.SeedAsync(ClientPolicyApp.Policy(AppPlatform.Android, "1.2.0", "1.2.0"));
        using var http = App.Client();
        using var request = Request(SessionPath, "android/1.1.0");
        request.Headers.Add("Origin", ClientPolicyApp.AndroidOrigin);

        using var response = await http.SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
        Assert.Equal(
            ClientPolicyApp.AndroidOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task PublicApiIsNotGated()
    {
        await App.SeedAsync(ClientPolicyApp.Policy(AppPlatform.Desktop, "2.0.0", "2.0.0"));
        using var http = App.Client();

        using var response = await SendAsync(http, "/v1/unknown", "desktop/1.0.0");

        Assert.NotEqual(HttpStatusCode.UpgradeRequired, response.StatusCode);
    }

    public async ValueTask InitializeAsync() => _app = await ClientPolicyApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient http,
        string path,
        string? client)
    {
        using var request = Request(path, client);
        return await http.SendAsync(request, Cancellation);
    }

    internal static HttpRequestMessage Request(string path, string? client)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (client is not null)
        {
            request.Headers.Add(ClientHeader, client);
        }

        return request;
    }
}
