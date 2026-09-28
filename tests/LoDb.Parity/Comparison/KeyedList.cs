using System.Text.Json.Nodes;

namespace LoDb.Parity.Comparison;

/// <summary>
/// A JSON list whose elements carry an identity (<c>id</c>, or <c>stat</c> for item stat
/// rows): compared element by element whatever their positions, then for order.
/// </summary>
public sealed class KeyedList
{
    private static readonly string[] KeyNames = ["id", "stat"];

    private KeyedList(List<(string Key, JsonObject Element)> elements)
    {
        Elements = elements;
        ByKey = elements.ToDictionary(static e => e.Key, static e => e.Element);
    }

    public IReadOnlyList<(string Key, JsonObject Element)> Elements { get; }

    public IReadOnlyDictionary<string, JsonObject> ByKey { get; }

    /// <summary>
    /// Both lists keyed by the same property, or false when either holds an element without
    /// it; an empty list takes the key of the other.
    /// </summary>
    public static bool TryPair(
        JsonArray legacy,
        JsonArray next,
        out (KeyedList Legacy, KeyedList Next) pair)
    {
        pair = default;
        var name = KeyNames.FirstOrDefault(name => AllKeyed(legacy, name) && AllKeyed(next, name));
        if (name is null)
        {
            return false;
        }

        pair = (Of(legacy, name), Of(next, name));
        return true;
    }

    /// <summary>
    /// A list keyed by <paramref name="name"/>, <c>id</c> by default. A repeated key (the
    /// empty skin ids of 0.151.2) gets its rank, <c>#2</c> and on, so that each occurrence
    /// pairs with the same occurrence on the other side.
    /// </summary>
    public static KeyedList Of(JsonArray array, string name = "id")
    {
        ArgumentNullException.ThrowIfNull(array);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var elements = new List<(string, JsonObject)>();
        foreach (var element in array.OfType<JsonObject>())
        {
            var key = KeyOf(element, name);
            var rank = seen[key] = seen.GetValueOrDefault(key) + 1;
            elements.Add((rank == 1 ? key : $"{key}#{rank}", element));
        }

        return new KeyedList(elements);
    }

    /// <summary>The keys both lists hold, in this list's order.</summary>
    public IEnumerable<string> SharedWith(KeyedList other) =>
        Elements.Select(static element => element.Key).Where(other.ByKey.ContainsKey);

    private static bool AllKeyed(JsonArray array, string name) =>
        array.All(element => element is JsonObject entry && entry[name] is JsonValue);

    private static string KeyOf(JsonObject element, string name) =>
        element[name] is JsonValue value ? value.ToString() : "";
}
