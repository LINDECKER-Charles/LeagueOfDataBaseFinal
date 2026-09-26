using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace LoDb.Desktop.Tests.Support;

/// <summary>
/// The calls the page makes to the host, as the front's <c>host</c> strategy will.
/// </summary>
internal static class HostCalls
{
    public static async Task<HttpResponseMessage> LoginAsync(
        this DesktopTestHost host,
        string password,
        bool rememberMe = false) =>
        await host.Client.PostAsJsonAsync(
            "/desktop/auth/login",
            new { identifier = FakeApi.Identifier, password, rememberMe },
            TestContext.Current.CancellationToken);

    public static async Task<JsonObject> ReadSessionAsync(this DesktopTestHost host) =>
        (await host.Client.GetFromJsonAsync<JsonObject>(
            "/desktop/auth/session",
            TestContext.Current.CancellationToken))!;

    /// <summary>The headers the fake API received for a request through the proxy.</summary>
    public static async Task<JsonObject> EchoAsync(this DesktopTestHost host) =>
        (await host.Client.GetFromJsonAsync<JsonObject>(
            FakeApi.EchoPath,
            TestContext.Current.CancellationToken))!;
}
