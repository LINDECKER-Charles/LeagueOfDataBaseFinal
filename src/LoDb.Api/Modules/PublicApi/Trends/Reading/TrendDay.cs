using System.Text.Json;

namespace LoDb.Api.Modules.PublicApi.Trends.Reading;

/// <summary>
/// Reads the <c>entities</c> map of a daily aggregate, <c>{"{type}:{id}": views}</c>, as
/// go-api decoded it.
/// </summary>
/// <remarks>
/// A day whose map is not an object of whole numbers is skipped whole, as go-api skipped a
/// file it could not decode; a day without a map counts, with no views.
/// </remarks>
internal static class TrendDay
{
    private const char TypeSeparator = ':';

    /// <summary>
    /// Adds the views of <paramref name="entityType"/> the day counted to
    /// <paramref name="views"/>, keyed by id.
    /// </summary>
    /// <returns>False when the day cannot be read: nothing is added then.</returns>
    public static bool TryAdd(string? entities, string entityType, Dictionary<string, long> views)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(views);
        if (entities is null)
        {
            return true;
        }

        List<KeyValuePair<string, long>> counted = [];
        try
        {
            using var document = JsonDocument.Parse(entities);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var entity in root.EnumerateObject())
            {
                if (!TryReadViews(entity.Value, out var count))
                {
                    return false;
                }

                if (IdOf(entity.Name, entityType) is { } id)
                {
                    counted.Add(new(id, count));
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        foreach (var (id, count) in counted)
        {
            views[id] = views.GetValueOrDefault(id) + count;
        }

        return true;
    }

    // "champion:Ahri" is Ahri for the champions; "champion:" is nobody.
    private static string? IdOf(string key, string entityType) =>
        key.Length > entityType.Length + 1
        && key.StartsWith(entityType, StringComparison.Ordinal)
        && key[entityType.Length] == TypeSeparator
            ? key[(entityType.Length + 1)..]
            : null;

    // A JSON null reads as zero, as Go leaves a map value it cannot set.
    private static bool TryReadViews(JsonElement value, out long count)
    {
        count = 0;
        return value.ValueKind switch
        {
            JsonValueKind.Null => true,
            JsonValueKind.Number => value.TryGetInt64(out count),
            _ => false,
        };
    }
}
