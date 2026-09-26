using System.Globalization;
using System.Net;
using System.Text.Json;

namespace LoDb.Api.Tests.PublicApi.Support;

/// <summary>An answer of <c>/v1</c>, read whole: its status, its rate limit and its body.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Limit"><c>X-RateLimit-Limit</c>, null when not sent.</param>
/// <param name="Remaining"><c>X-RateLimit-Remaining</c>, null when not sent.</param>
/// <param name="Reset"><c>X-RateLimit-Reset</c>, null when not sent.</param>
/// <param name="Body">The JSON body; undefined when the answer is not JSON.</param>
public sealed record V1Answer(
    HttpStatusCode Status,
    int? Limit,
    int? Remaining,
    long? Reset,
    JsonElement Body)
{
    private const string JsonMediaType = "application/json";

    /// <summary>The <c>error.code</c> of a refusal, null otherwise.</summary>
    public string? ErrorCode =>
        Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("error", out var error)
            ? error.GetProperty("code").GetString()
            : null;

    public static async Task<V1Answer> ReadAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        JsonElement body = default;
        if (response.Content.Headers.ContentType?.MediaType == JsonMediaType)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(V1Server.Token);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: V1Server.Token);
            body = document.RootElement.Clone();
        }

        return new V1Answer(
            response.StatusCode,
            (int?)Header(response, "X-RateLimit-Limit"),
            (int?)Header(response, "X-RateLimit-Remaining"),
            Header(response, "X-RateLimit-Reset"),
            body);
    }

    private static long? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? long.Parse(values.Single(), CultureInfo.InvariantCulture)
            : null;
}
