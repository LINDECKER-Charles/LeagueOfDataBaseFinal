using System.Text.Json.Nodes;
using LoDb.Parity.Deviations;
using LoDb.Parity.Projections;

namespace LoDb.Parity.Runs;

/// <summary>
/// The <see cref="DeviationTags"/> of manifest keys, read from the new projections of a
/// version: which champion file is a portrait or an ability icon, which item is debris.
/// </summary>
public sealed class KeyTags
{
    private static readonly IReadOnlyDictionary<string, string> TypeOfResource =
        new Dictionary<string, string>
        {
            ["champions"] = "champion",
            ["items"] = "item",
            ["runes"] = "runesReforged",
            ["summoners"] = "summoner",
        };

    private readonly Dictionary<(string Type, string Key), HashSet<string>> tags = [];

    /// <summary>Tags of the keys of one manifest type.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Of(string type) =>
        tags.Where(pair => pair.Key.Type == type)
            .ToDictionary(pair => pair.Key.Key, pair => (IReadOnlySet<string>)pair.Value);

    /// <summary>
    /// Marks the passive and ability icons of the champions a legacy projection holds the
    /// detail of: the legacy stack stored those icons when their page was visited.
    /// </summary>
    public void AddLegacyDetails(JsonObject projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        foreach (var entry in projection["champions"]?["entries"]?.AsArray() ?? [])
        {
            if (entry?["detail"]?.GetValue<bool>() != true)
            {
                continue;
            }

            Tag("champion", entry["passive"]?["image"], [DeviationTags.Detailed]);
            foreach (var spell in entry["spells"]?.AsArray() ?? [])
            {
                Tag("champion", spell?["image"], [DeviationTags.Detailed]);
            }
        }
    }

    /// <summary>Records the images of a new projection, whatever its language.</summary>
    public void Add(JsonObject projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        foreach (var (resource, type) in TypeOfResource)
        {
            foreach (var entry in projection[resource]?["entries"]?.AsArray() ?? [])
            {
                AddEntry(type, entry as JsonObject);
            }
        }
    }

    private void AddEntry(string type, JsonObject? entry)
    {
        var entryTags = EntryTags.Of(null, entry, fallback: false);
        Tag(type, entry?["image"], [.. entryTags, .. PortraitOf(type)]);
        Tag(type, entry?["passive"]?["image"], [DeviationTags.Ability]);
        foreach (var spell in entry?["spells"]?.AsArray() ?? [])
        {
            Tag(type, spell?["image"], [DeviationTags.Ability]);
        }

        foreach (var rune in entry?["slots"]?.AsArray().SelectMany(slot => slot!.AsArray()) ?? [])
        {
            Tag(type, rune?["image"], []);
        }
    }

    private static string[] PortraitOf(string type) =>
        type == "champion" ? [DeviationTags.Portrait] : [];

    private void Tag(string type, JsonNode? image, IEnumerable<string> names)
    {
        if (image?["file"]?.GetValue<string>() is not { } file)
        {
            return;
        }

        var key = (type, file);
        if (!tags.TryGetValue(key, out var set))
        {
            tags[key] = set = new HashSet<string>(StringComparer.Ordinal);
        }

        set.UnionWith(names);
    }
}
