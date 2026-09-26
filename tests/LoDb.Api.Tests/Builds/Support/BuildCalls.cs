using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.Accounts.Support;

namespace LoDb.Api.Tests.Builds.Support;

/// <summary>The calls of the build pages, sent as the Angular front sends them.</summary>
public static class BuildCalls
{
    public const string Builds = "/api/builds";
    public const string Share = "/api/share/";
    public const string Trends = "/api/trends";

    public static string Of(int id) => $"{Builds}/{id}";

    public static string Of(JsonElement build) => Of(build.GetProperty("id").GetInt32());

    /// <summary>
    /// A build every rule accepts on the latest patch: Ahri, Precision and Domination, four
    /// items in two steps.
    /// </summary>
    public static JsonObject ValidBody() => new()
    {
        ["name"] = "Ahri mid burst",
        ["description"] = "  Roam after six.  ",
        ["isPublic"] = true,
        ["gameVersion"] = BuildsApp.Latest.Value,
        ["gameMode"] = "sr",
        ["language"] = "en_US",
        ["structure"] = new JsonObject
        {
            ["championId"] = " Ahri ",
            ["runes"] = new JsonObject
            {
                ["primaryStyleId"] = 8000,
                ["primarySelections"] = new JsonArray(8005, 9101, 9104, 8014),
                ["secondaryStyleId"] = 8100,
                ["secondarySelections"] = new JsonArray(8126, 8137),
            },
            ["steps"] = new JsonArray(
                Step("Start", null, "1001", "2003"),
                Step(" Core ", "  Rush it  ", "3078", "3006")),
        },
    };

    public static JsonObject Step(string label, string? note, params string[] items) => new()
    {
        ["label"] = label,
        ["note"] = note,
        ["items"] = new JsonArray([.. items.Select(static id => JsonValue.Create(id))]),
    };

    /// <summary>The structure of <paramref name="body"/>, to change before sending it.</summary>
    public static JsonObject Structure(JsonObject body) => body["structure"]!.AsObject();

    /// <summary>A PUT, DELETE or other unsafe call with the page's Origin and XSRF token.</summary>
    public static async Task<HttpResponseMessage> SendAsync(
        this BrowserClient browser,
        HttpMethod method,
        string path,
        object? body = null)
    {
        ArgumentNullException.ThrowIfNull(browser);
        using var request = browser.Post(path, body);
        request.Method = method;
        return await browser.SendAsync(request);
    }

    /// <summary>Creates a build that must be accepted, and returns it.</summary>
    public static async Task<JsonElement> CreateAsync(this BrowserClient browser, JsonObject body)
    {
        ArgumentNullException.ThrowIfNull(browser);
        using var response = await browser.PostAsync(Builds, body);
        return await ApiJson.ReadAsync(response, HttpStatusCode.Created);
    }

    public static Task<HttpResponseMessage> VoteAsync(
        this BrowserClient browser,
        int buildId,
        string? value) =>
        browser.PostAsync(Of(buildId) + "/vote", new { value });

    /// <summary>The body of a 200 answer to a GET.</summary>
    public static async Task<JsonElement> GetJsonAsync(this BrowserClient browser, string path)
    {
        ArgumentNullException.ThrowIfNull(browser);
        using var response = await browser.GetAsync(path);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    /// <summary>The <c>id</c> of each element of a JSON array.</summary>
    public static IReadOnlyList<int> Ids(JsonElement array) =>
        [.. array.EnumerateArray().Select(static entry => entry.GetProperty("id").GetInt32())];
}
