using System.Text.Json;
using System.Text.RegularExpressions;

namespace LoDb.Api.Tests.PublicApi.Support;

/// <summary>
/// go-api's <c>/v1</c> contract recorded by L6.1 (<c>tests/fixtures/v1</c>), copied next to
/// the test assembly: its data set, its keys and its recorded answers.
/// </summary>
internal static partial class V1Fixtures
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> LoadedKeys = new(LoadKeys);

    public static string Directory { get; } =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "v1");

    /// <summary>The Data Dragon datasets go-api named its trends from, by version.</summary>
    public static string StorageData { get; } = Path.Combine(Directory, "seed", "storage", "data");

    public static string Seed(string file) => Path.Combine(Directory, "seed", file);

    public static string Reference(string group) =>
        Path.Combine(Directory, "references", group + ".json");

    /// <summary>The raw key of an alias of <c>keys.json</c>.</summary>
    public static string Key(string alias) => LoadedKeys.Value[alias];

    /// <summary>A text with every <c>{{key:alias}}</c> replaced by its key.</summary>
    public static string Resolve(string text) =>
        KeyPlaceholder().Replace(text, static match => Key(match.Groups["alias"].Value));

    private static Dictionary<string, string> LoadKeys()
    {
        using var keys = JsonDocument.Parse(File.ReadAllText(Seed("keys.json")));
        return keys.RootElement.GetProperty("keys").EnumerateObject()
            .ToDictionary(static key => key.Name, static key => key.Value.GetString()!);
    }

    [GeneratedRegex(@"\{\{key:(?<alias>[a-z_]+)\}\}")]
    private static partial Regex KeyPlaceholder();
}
