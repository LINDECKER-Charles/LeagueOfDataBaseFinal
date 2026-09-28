using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace LoDb.Parity.Comparison;

/// <summary>Equality and rendering of JSON values, as both stacks write them.</summary>
public static class JsonValues
{
    private const int MaxRendered = 300;

    // Deviations are read in the report: scripts and HTML characters are kept as they are.
    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Deep equality where numbers compare by value: PHP keeps <c>25.0</c> for a float,
    /// .NET writes <c>25</c> for the same double.
    /// </summary>
    public static bool Equal(JsonNode? legacy, JsonNode? next)
    {
        if (legacy is JsonValue left && next is JsonValue right
            && left.GetValueKind() == JsonValueKind.Number
            && right.GetValueKind() == JsonValueKind.Number)
        {
            return NumberOf(left) == NumberOf(right);
        }

        return JsonNode.DeepEquals(legacy, next);
    }

    /// <summary>A short text for the report: compact JSON, an image as its verdict.</summary>
    public static string Render(JsonNode? node)
    {
        if (node is JsonObject image && ImageVerdict.IsImage(image))
        {
            return ImageVerdict.Of(image);
        }

        var text = node is null ? "null" : node.ToJsonString(Compact);
        return text.Length <= MaxRendered
            ? text
            : string.Concat(text.AsSpan(0, MaxRendered), "…");
    }

    private static decimal? NumberOf(JsonValue value) =>
        value.TryGetValue<decimal>(out var number) ? number : null;
}
