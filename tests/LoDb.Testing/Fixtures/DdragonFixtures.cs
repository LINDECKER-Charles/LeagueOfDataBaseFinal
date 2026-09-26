using System.Text.Json;
using LoDb.Domain.Versions;

namespace LoDb.Testing.Fixtures;

/// <summary>
/// The recorded Data Dragon and CommunityDragon answers, copied next to the test assembly.
/// </summary>
/// <remarks>
/// Recorded by <c>node tools/next/fixtures/record.mjs</c> into <c>tests/fixtures/ddragon</c>:
/// the two newest versions of the recording day in full, and the trap versions (0.151.2,
/// 3.6.14, 3.13.8, 3.13.24, 7.21.1, 7.22.1, 8.7.1) for their datasets and rune icons.
/// </remarks>
public static class DdragonFixtures
{
    private const string IndexFile = "index.json";
    private const string DatasetSegment = "data";
    private const int VersionSegmentIndex = 1;

    private static readonly JsonSerializerOptions IndexOptions = new(JsonSerializerDefaults.Web);

    private static readonly Lazy<FixtureIndex> LoadedIndex = new(Load);

    /// <summary>The recording's directory in the test output.</summary>
    public static string Directory { get; } =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "ddragon");

    public static FixtureIndex Index => LoadedIndex.Value;

    /// <summary>The newest recorded version, recorded in full.</summary>
    public static PatchVersion Latest => PatchVersion.Parse(Index.Roles.Latest);

    /// <summary>The version before <see cref="Latest"/>, recorded in full.</summary>
    public static PatchVersion Previous => PatchVersion.Parse(Index.Roles.Previous);

    /// <summary>Every version whose datasets are recorded, newest first.</summary>
    public static IReadOnlyList<PatchVersion> Versions =>
        [.. Index.Responses
            .Select(static response => DatasetVersion(new Uri(response.Url)))
            .OfType<PatchVersion>()
            .Distinct()
            .OrderDescending()];

    /// <summary>A replay of the whole recording, with its own request log.</summary>
    public static FixtureReplayHandler CreateReplay() => new(Index, Directory);

    /// <summary>Where the body of <paramref name="url"/> lives: host, then path.</summary>
    public static string BodyPath(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return FixtureReplayHandler.BodyPath(Directory, url);
    }

    private static FixtureIndex Load()
    {
        using var index = File.OpenRead(Path.Combine(Directory, IndexFile));
        return JsonSerializer.Deserialize<FixtureIndex>(index, IndexOptions)
            ?? throw new InvalidDataException($"{IndexFile} is empty.");
    }

    // "cdn/{version}/data/{lang}/{file}.json" names the version of a dataset.
    private static PatchVersion? DatasetVersion(Uri url)
    {
        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > VersionSegmentIndex + 1
            && segments[VersionSegmentIndex + 1] == DatasetSegment
            && PatchVersion.TryParse(segments[VersionSegmentIndex], out var version)
                ? version
                : null;
    }
}
