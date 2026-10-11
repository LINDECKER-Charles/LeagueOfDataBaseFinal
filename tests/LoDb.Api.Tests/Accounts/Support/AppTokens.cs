using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>
/// The account calls of a native app: no cookie, no <c>Origin</c>, the access token in the
/// <c>Authorization</c> header, and the refresh token traded at <c>/refresh</c>.
/// </summary>
public static class AppTokens
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>Signs the account in for tokens; fails the test otherwise.</summary>
    public static async Task<JsonElement> SignInAsync(
        HttpClient app,
        string identifier = AccountSeed.Name)
    {
        ArgumentNullException.ThrowIfNull(app);
        using var response = await app.PostAsJsonAsync(
            Path("token"),
            new { identifier, password = AccountSeed.StrongPassword },
            Cancellation);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient app, JsonElement tokens)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.PostAsJsonAsync(
            Path("refresh"),
            new { refreshToken = ApiJson.Text(tokens, "refreshToken") },
            Cancellation);
    }

    /// <summary>Refreshes; fails the test unless new tokens come back.</summary>
    public static async Task<JsonElement> RenewAsync(HttpClient app, JsonElement tokens)
    {
        using var response = await RefreshAsync(app, tokens);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    /// <summary>A call of the app with its access token.</summary>
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient app,
        HttpMethod method,
        string path,
        JsonElement tokens)
    {
        ArgumentNullException.ThrowIfNull(app);
        using var request = new HttpRequestMessage(method, Path(path));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ApiJson.Text(tokens, "accessToken"));
        return await app.SendAsync(request, Cancellation);
    }

    private static Uri Path(string path) => new($"/api/account/{path}", UriKind.Relative);
}
