using System.Text.Json;
using LoDb.Domain.Builds.Structures;

namespace LoDb.Api.Modules.Builds.Storage;

/// <summary>
/// Reads the JSON of a build column as leniently as the legacy stack decoded it: a value of
/// the wrong shape reads as absent, never as a failure, so that an old row still renders.
/// </summary>
internal static class StoredJson
{
    // PHP decodes both {} and [] to an array: a list is a record whose named keys are absent.
    public static bool IsRecord(JsonElement element) =>
        element.ValueKind is JsonValueKind.Object or JsonValueKind.Array;

    /// <summary>The named value of a record; null when absent or when it is a list.</summary>
    public static JsonElement? Field(JsonElement record, string name) =>
        record.ValueKind == JsonValueKind.Object && record.TryGetProperty(name, out var value)
            ? value
            : null;

    /// <summary>An integer, or the canonical decimal text of one; null otherwise.</summary>
    public static int? Integer(JsonElement? value) => value switch
    {
        { ValueKind: JsonValueKind.Number } number when number.TryGetInt32(out var parsed) =>
            parsed,
        { ValueKind: JsonValueKind.String } text => IntegerText.Read(text.GetString()),
        _ => null,
    };

    /// <summary>A scalar as PHP casts it to a string; null for a list, a record or null.</summary>
    public static string? Scalar(JsonElement? value) => value switch
    {
        { ValueKind: JsonValueKind.String } text => text.GetString(),
        { ValueKind: JsonValueKind.Number } number => number.GetRawText(),
        { ValueKind: JsonValueKind.True } => "1",
        { ValueKind: JsonValueKind.False } => string.Empty,
        _ => null,
    };

    /// <summary>The entries of a list, each read by <paramref name="read"/>; else null.</summary>
    public static IReadOnlyList<TEntry>? List<TEntry>(
        JsonElement? value,
        Func<JsonElement, TEntry> read) =>
        value is { ValueKind: JsonValueKind.Array } list
            ? [.. list.EnumerateArray().Select(read)]
            : null;
}
