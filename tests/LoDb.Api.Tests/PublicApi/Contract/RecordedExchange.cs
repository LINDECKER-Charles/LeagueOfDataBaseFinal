using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.PublicApi.Support;

namespace LoDb.Api.Tests.PublicApi.Contract;

/// <summary>
/// A step of a recorded group, in order: a request and go-api's answer, or an action on the
/// running service (<c>sleep</c>, <c>sql</c>, <c>stopDatabase</c>, <c>hideStorage</c>).
/// </summary>
/// <remarks>A repeated request comes once a repetition: <c>burst#1</c> to <c>#n</c>.</remarks>
internal sealed record RecordedExchange(
    string Id,
    string? Action,
    int? Milliseconds,
    string? Sql,
    RecordedRequest? Request,
    RecordedResponse? Response)
{
    public static IReadOnlyList<RecordedExchange> LoadGroup(string group)
    {
        using var reference = JsonDocument.Parse(File.ReadAllText(V1Fixtures.Reference(group)));
        return [.. reference.RootElement.GetProperty("exchanges").EnumerateArray().Select(Read)];
    }

    private static RecordedExchange Read(JsonElement step) => new(
        step.GetProperty("id").GetString()!,
        Optional(step, "action")?.GetString(),
        Optional(step, "ms")?.GetInt32(),
        Optional(step, "sql")?.GetString(),
        Optional(step, "request") is { } request ? ReadRequest(request) : null,
        Optional(step, "response") is { } response ? ReadResponse(response) : null);

    private static RecordedRequest ReadRequest(JsonElement request) => new(
        request.GetProperty("method").GetString()!,
        request.GetProperty("path").GetString()!,
        Strings(Optional(request, "headers")));

    private static RecordedResponse ReadResponse(JsonElement response) => new(
        response.GetProperty("status").GetInt32(),
        Strings(Optional(response, "headers")),
        Optional(response, "body") is { } body ? JsonNode.Parse(body.GetRawText()) : null,
        Optional(response, "text")?.GetString());

    private static SortedDictionary<string, string> Strings(JsonElement? values)
    {
        var strings = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (values is { } found)
        {
            foreach (var value in found.EnumerateObject())
            {
                strings[value.Name] = value.Value.GetString()!;
            }
        }

        return strings;
    }

    private static JsonElement? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value : null;
}

/// <summary>A request as the capture sent it, keys written <c>{{key:alias}}</c>.</summary>
internal sealed record RecordedRequest(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers);
