using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.PublicApi.Support;

namespace LoDb.Api.Tests.PublicApi.Contract;

/// <summary>
/// An answer normalized as the capture of L6.1 recorded go-api's: the status, the headers
/// the contract names, then the body decoded when it is JSON, its text otherwise.
/// </summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Headers">
/// The recorded headers present, by lowercase name; <c>x-ratelimit-reset</c> reads
/// <c>&lt;unix-time&gt;</c> when it falls between the start of the request and a minute
/// after its end.
/// </param>
/// <param name="Body">The JSON body; null when it is not JSON or empty.</param>
/// <param name="Text">Any other body, byte for byte; null when it is JSON or empty.</param>
internal sealed record RecordedResponse(
    int Status,
    IReadOnlyDictionary<string, string> Headers,
    JsonNode? Body,
    string? Text)
{
    public const string ResetHeader = "x-ratelimit-reset";
    public const string UnixTime = "<unix-time>";
    public const string InvalidUnixTime = "<invalid-unix-time>";

    /// <summary>Every header the capture kept, in lowercase.</summary>
    public static IReadOnlyList<string> RecordedHeaders { get; } =
    [
        "content-type",
        "x-content-type-options",
        "allow",
        "location",
        "access-control-allow-origin",
        "access-control-allow-methods",
        "access-control-allow-headers",
        "x-ratelimit-limit",
        "x-ratelimit-remaining",
        ResetHeader,
    ];

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>The answer of the new API, read as the capture read go-api's.</summary>
    public static async Task<RecordedResponse> ReadAsync(
        HttpResponseMessage response,
        DateTimeOffset started,
        DateTimeOffset ended)
    {
        ArgumentNullException.ThrowIfNull(response);
        var sent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in response.Headers.NonValidated)
        {
            sent[name] = values.ToString();
        }

        foreach (var (name, values) in response.Content.Headers.NonValidated)
        {
            sent[name] = values.ToString();
        }

        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in RecordedHeaders)
        {
            if (sent.TryGetValue(name, out var value))
            {
                headers[name] = name == ResetHeader ? Reset(value, started, ended) : value;
            }
        }

        var status = (int)response.StatusCode;
        var bytes = await response.Content.ReadAsByteArrayAsync(V1Server.Token);
        if (bytes.Length == 0)
        {
            return new(status, headers, null, null);
        }

        return response.Content.Headers.ContentType?.MediaType == "application/json"
            ? new(status, headers, JsonNode.Parse(bytes), null)
            : new(status, headers, null, Encoding.UTF8.GetString(bytes));
    }

    /// <summary>What sets <paramref name="actual"/> apart from this answer, if anything.</summary>
    public IEnumerable<string> DifferencesFrom(RecordedResponse actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (Status != actual.Status)
        {
            yield return $"status: expected {Status}, got {actual.Status}";
        }

        foreach (var name in RecordedHeaders)
        {
            var expected = Headers.GetValueOrDefault(name);
            var got = actual.Headers.GetValueOrDefault(name);
            if (!string.Equals(expected, got, StringComparison.Ordinal))
            {
                yield return $"{name}: expected {Quote(expected)}, got {Quote(got)}";
            }
        }

        if (!JsonNode.DeepEquals(Body, actual.Body))
        {
            yield return $"body: expected {Show(Body)}\n      got {Show(actual.Body)}";
        }

        if (!string.Equals(Text, actual.Text, StringComparison.Ordinal))
        {
            yield return $"text: expected {Quote(Text)}, got {Quote(actual.Text)}";
        }
    }

    private static string Reset(string value, DateTimeOffset started, DateTimeOffset ended) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var unix)
        && unix >= started.ToUnixTimeSeconds()
        && unix <= ended.AddMinutes(1).ToUnixTimeSeconds()
            ? UnixTime
            : InvalidUnixTime;

    private static string Quote(string? value) =>
        value is null ? "(none)" : JsonSerializer.Serialize(value);

    private static string Show(JsonNode? body) =>
        body is null ? "(none)" : body.ToJsonString(Indented);
}
