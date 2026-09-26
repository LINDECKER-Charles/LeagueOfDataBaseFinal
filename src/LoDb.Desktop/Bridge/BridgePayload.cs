using System.Text.Json;

namespace LoDb.Desktop.Bridge;

/// <summary>Reads the fields of a payload without trusting their JSON type.</summary>
internal static class BridgePayload
{
    /// <summary>The field when it is a string; null when absent or of another type.</summary>
    public static string? StringOf(JsonElement payload, string field) =>
        payload.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
