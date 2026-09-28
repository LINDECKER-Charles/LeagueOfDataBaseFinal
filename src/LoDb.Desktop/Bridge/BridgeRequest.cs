using System.Text.Json;

namespace LoDb.Desktop.Bridge;

/// <summary>A message of the page: <c>{id, type, payload}</c> (plan §5.2).</summary>
internal sealed record BridgeRequest
{
    /// <summary>Longest id accepted: the page's correlation ids are short.</summary>
    public const int MaxIdLength = 64;

    private const string IdMember = "id";
    private const string TypeMember = "type";
    private const string PayloadMember = "payload";

    // A message without payload reads as an empty one: updateState and applyUpdate need none.
    private static readonly JsonElement EmptyPayload = JsonSerializer.SerializeToElement(new { });

    public required string Id { get; init; }

    /// <summary>Null when the message has no string <c>type</c>.</summary>
    public required string? Type { get; init; }

    public required JsonElement Payload { get; init; }

    /// <summary>
    /// The request, or null when the message has no usable id: without one, no reply could
    /// reach the caller, so the message is dropped.
    /// </summary>
    public static BridgeRequest? From(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object
            || BridgePayload.StringOf(message, IdMember)
                is not { Length: > 0 and <= MaxIdLength } id)
        {
            return null;
        }

        return new BridgeRequest
        {
            Id = id,
            Type = BridgePayload.StringOf(message, TypeMember),
            Payload = message.TryGetProperty(PayloadMember, out var payload)
                ? payload
                : EmptyPayload,
        };
    }
}
