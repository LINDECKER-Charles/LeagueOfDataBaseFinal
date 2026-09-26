using System.Text.Json.Nodes;
using LoDb.Parity.Deviations;

namespace LoDb.Parity.Projections;

/// <summary>The <see cref="DeviationTags"/> an entry gives the deviations found in it.</summary>
public static class EntryTags
{
    /// <summary>Tags of an entry seen on either side, and of its dataset.</summary>
    public static IReadOnlySet<string> Of(JsonObject? legacy, JsonObject? next, bool fallback)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);
        AddWhen(tags, DeviationTags.Fallback, fallback);
        AddWhen(tags, DeviationTags.Detailed, legacy?["detail"] is JsonValue detail
            && detail.GetValue<bool>());
        AddWhen(tags, DeviationTags.Unlisted, IsFalse(legacy, "listed") || IsFalse(next, "listed"));
        AddWhen(tags, DeviationTags.Classic, Is(legacy, "edition", "classic")
            || Is(next, "edition", "classic"));
        return tags;
    }

    private static void AddWhen(HashSet<string> tags, string tag, bool condition)
    {
        if (condition)
        {
            tags.Add(tag);
        }
    }

    private static bool IsFalse(JsonObject? entry, string name) =>
        entry?[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && !flag;

    private static bool Is(JsonObject? entry, string name, string expected) =>
        entry?[name] is JsonValue value && value.TryGetValue<string>(out var text)
        && text == expected;
}
