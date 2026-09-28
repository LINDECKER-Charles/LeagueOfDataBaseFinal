using System.Globalization;
using System.Text.Json;

namespace LoDb.Infrastructure.Analytics.Aggregation;

/// <summary>
/// Reads the parts of a daily aggregate leniently, as PHP wrote them: an empty map may be a
/// list (<c>[]</c>), a list stands for a map keyed <c>0…n</c>, and an all-digit visitor hash
/// became a number when PHP used it as an array key.
/// </summary>
internal static class DailyJson
{
    /// <summary>Adds each <c>{key: count}</c> of a map (or a PHP list) to a bucket.</summary>
    public static void ReadCounters(JsonElement map, DailyAggregate daily, string bucket)
    {
        ArgumentNullException.ThrowIfNull(daily);
        foreach (var (key, value) in Entries(map))
        {
            if (TryCount(value, out var count))
            {
                daily.Count(bucket, key, count);
            }
        }
    }

    /// <summary>Adds the counts of a vector, or of its map form, to a target.</summary>
    public static void ReadVector(JsonElement vector, long[] target)
    {
        ArgumentNullException.ThrowIfNull(target);
        foreach (var (key, value) in Entries(vector))
        {
            if (int.TryParse(key, CultureInfo.InvariantCulture, out var index)
                && index >= 0 && index < target.Length
                && TryCount(value, out var count))
            {
                target[index] += count;
            }
        }
    }

    /// <summary>Adds the visitor hashes of a list, the numbers read back as their digits.</summary>
    public static void ReadVisitors(JsonElement list, DailyAggregate daily)
    {
        ArgumentNullException.ThrowIfNull(daily);
        foreach (var (_, value) in Entries(list))
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    daily.AddVisitor(value.GetString()!);
                    break;
                case JsonValueKind.Number:
                    daily.AddVisitor(value.GetRawText());
                    break;
            }
        }
    }

    /// <summary>Adds the <c>{code: name}</c> of a map; the first name of a code stays.</summary>
    public static void ReadNames(JsonElement map, IDictionary<string, string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        foreach (var (key, value) in Entries(map))
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                names.TryAdd(key, value.GetString()!);
            }
        }
    }

    /// <summary>A count; a fractional one is truncated, as PHP's <c>(int)</c> does.</summary>
    public static bool TryCount(JsonElement value, out long count)
    {
        count = 0;
        if (value.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        count = value.TryGetInt64(out var whole) ? whole : (long)value.GetDouble();
        return true;
    }

    private static IEnumerable<(string Key, JsonElement Value)> Entries(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .Select(static property => (property.Name, property.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(static (value, index) =>
                    (index.ToString(CultureInfo.InvariantCulture), value)),
            _ => [],
        };
}
