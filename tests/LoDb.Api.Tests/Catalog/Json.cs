using System.Text.Json;

namespace LoDb.Api.Tests.Catalog;

/// <summary>Reading helpers over the answers, kept to what the assertions need.</summary>
internal static class Json
{
    public static string Text(this JsonElement element, string property) =>
        element.GetProperty(property).GetString()
        ?? throw new InvalidOperationException($"{property} is null.");

    public static IReadOnlyList<JsonElement> Items(this JsonElement element, string property) =>
        [.. element.GetProperty(property).EnumerateArray()];

    public static IReadOnlyList<string> Texts(this JsonElement element, string property) =>
        [.. element.GetProperty(property).EnumerateArray().Select(static item =>
            item.GetString() ?? string.Empty)];

    /// <summary>The <paramref name="property"/> of every entry of the array.</summary>
    public static IReadOnlyList<string> Pluck(
        this JsonElement element,
        string array,
        string property) =>
        [.. element.Items(array).Select(item => item.GetProperty(property).ToString())];

    /// <summary>The entry of the array whose <c>id</c> is <paramref name="id"/>.</summary>
    public static JsonElement Entry(this JsonElement element, string array, string id) =>
        element.Items(array).Single(item =>
            string.Equals(item.GetProperty("id").ToString(), id, StringComparison.Ordinal));

    public static bool IsNull(this JsonElement element, string property) =>
        !element.TryGetProperty(property, out var value)
        || value.ValueKind == JsonValueKind.Null;
}
