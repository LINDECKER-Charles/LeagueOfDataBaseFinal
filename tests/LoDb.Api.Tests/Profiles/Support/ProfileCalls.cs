using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.Accounts.Support;

namespace LoDb.Api.Tests.Profiles.Support;

/// <summary>The calls of the profile pages, sent as the Angular front sends them.</summary>
public static class ProfileCalls
{
    public const string Own = "/api/profile";
    public const string Preview = Own + "/preview";
    public const string Favorites = Own + "/favorites";
    public const string Visibility = Own + "/visibility";
    public const string Version = Own + "/version";
    public const string Identity = Own + "/identity";
    public const string Password = Own + "/password";
    public const string Delete = Own + "/delete";
    public const string Public = "/api/profiles/";

    private const string TraceId = "traceId";

    /// <summary>A PUT with the page's <c>Origin</c> and XSRF token.</summary>
    public static async Task<HttpResponseMessage> PutAsync(
        this BrowserClient browser,
        string path,
        object body)
    {
        ArgumentNullException.ThrowIfNull(browser);
        using var request = browser.Post(path, body);
        request.Method = HttpMethod.Put;
        return await browser.SendAsync(request);
    }

    /// <summary>The body of a 200 answer to a GET.</summary>
    public static async Task<JsonElement> GetJsonAsync(this BrowserClient browser, string path)
    {
        ArgumentNullException.ThrowIfNull(browser);
        using var response = await browser.GetAsync(path);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    /// <summary>Saves the five slots; a null clears one.</summary>
    public static Task<HttpResponseMessage> SaveFavoritesAsync(
        this BrowserClient browser,
        FavoritesBody body) =>
        browser.PutAsync(Favorites, body);

    /// <summary>
    /// The body of an answer without its <c>traceId</c>, the only part that differs from one
    /// request to the next.
    /// </summary>
    public static async Task<string> UntracedBodyAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var body = await response.Content.ReadAsStringAsync(ProfilesApp.Token);
        var node = JsonNode.Parse(body)!.AsObject();
        node.Remove(TraceId);
        return node.ToJsonString();
    }

    /// <summary>The status of a favorite slot of a showcase.</summary>
    public static string? SlotStatus(JsonElement showcase, string slot) =>
        showcase.GetProperty("favorites").GetProperty(slot).GetProperty("status").GetString();

    /// <summary>The current name of a favorite slot of a showcase.</summary>
    public static string? SlotName(JsonElement showcase, string slot) =>
        showcase.GetProperty("favorites").GetProperty(slot)
            .GetProperty("current").GetProperty("name").GetString();
}
