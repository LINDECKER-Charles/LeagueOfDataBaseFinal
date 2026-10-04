using System.Text.Json;

namespace LoDb.Api.Modules.Analytics.Capture;

/// <summary>
/// Reads the path the router's beacon posts: <c>{"path":"/fr/items?lang=fr_FR"}</c>, sent by
/// <c>navigator.sendBeacon</c> as <c>text/plain</c>, which needs no preflight.
/// </summary>
internal static class BeaconBody
{
    // A path and its query, with room to spare: the body is read whole into memory.
    public const int MaxBytes = 4096;

    private const string PathProperty = "path";

    /// <returns>The path, or null when the body is too long or not such an object.</returns>
    public static async Task<string?> ReadPathAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentLength > MaxBytes)
        {
            return null;
        }

        var buffer = new byte[MaxBytes + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await request.Body.ReadAsync(buffer.AsMemory(length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        return length > MaxBytes ? null : PathOf(buffer.AsMemory(0, length));
    }

    private static string? PathOf(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(PathProperty, out var path)
                && path.ValueKind == JsonValueKind.String
                ? path.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
