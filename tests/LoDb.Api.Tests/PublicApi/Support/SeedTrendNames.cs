using System.Globalization;
using System.Text.Json;
using LoDb.Api.Modules.PublicApi.Trends;
using LoDb.Api.Modules.PublicApi.Trends.Reading;

namespace LoDb.Api.Tests.PublicApi.Support;

/// <summary>
/// The names go-api gave the trends, read as it read them: the en_US datasets of the newest
/// version of the seed storage, the rune paths and the runes by id and by key.
/// </summary>
/// <remarks>
/// Stands in for the catalog in the contract tests, whose recorded names come from these
/// reduced datasets; <c>CatalogTrendNamesTests</c> covers the catalog itself.
/// </remarks>
internal sealed class SeedTrendNames : ITrendNames
{
    private const string Language = "en_US";

    private static readonly Lazy<Dictionary<TrendType, IReadOnlyDictionary<string, string>>>
        Names = new(Load);

    public static SeedTrendNames Instance { get; } = new();

    public Task<IReadOnlyDictionary<string, string>> NamesAsync(
        TrendType type,
        CancellationToken cancellationToken) =>
        Task.FromResult(Names.Value[type]);

    private static Dictionary<TrendType, IReadOnlyDictionary<string, string>> Load()
    {
        // Numerically: 16.18.1 is newer than 16.9.1, whose names are stale on purpose.
        var latest = Directory.EnumerateDirectories(V1Fixtures.StorageData)
            .Select(Path.GetFileName)
            .OrderByDescending(static version => Version.Parse(version!))
            .First()!;
        var folder = Path.Combine(V1Fixtures.StorageData, latest, Language);
        return new Dictionary<TrendType, IReadOnlyDictionary<string, string>>
        {
            [TrendType.Champions] = DataNames(Path.Combine(folder, "champion.json")),
            [TrendType.Items] = DataNames(Path.Combine(folder, "item.json")),
            [TrendType.Summoners] = DataNames(Path.Combine(folder, "summoner.json")),
            [TrendType.Runes] = RuneNames(Path.Combine(folder, "runesReforged.json")),
        };
    }

    // champion.json, item.json and summoner.json: {"data": {id: {"name": …}}}.
    private static Dictionary<string, string> DataNames(string file)
    {
        using var dataset = JsonDocument.Parse(File.ReadAllText(file));
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in dataset.RootElement.GetProperty("data").EnumerateObject())
        {
            Add(names, entry.Name, entry.Value.GetProperty("name").GetString());
        }

        return names;
    }

    // runesReforged.json: the paths, each with its runes in slots, by id and by key.
    private static Dictionary<string, string> RuneNames(string file)
    {
        using var dataset = JsonDocument.Parse(File.ReadAllText(file));
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tree in dataset.RootElement.EnumerateArray())
        {
            AddRune(names, tree);
            foreach (var slot in tree.GetProperty("slots").EnumerateArray())
            {
                foreach (var rune in slot.GetProperty("runes").EnumerateArray())
                {
                    AddRune(names, rune);
                }
            }
        }

        return names;
    }

    private static void AddRune(Dictionary<string, string> names, JsonElement rune)
    {
        var name = rune.GetProperty("name").GetString();
        Add(names, rune.GetProperty("key").GetString(), name);
        Add(
            names,
            rune.GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture),
            name);
    }

    private static void Add(Dictionary<string, string> names, string? id, string? name)
    {
        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
        {
            names[id] = name;
        }
    }
}
