using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Parity.Deviations;
using LoDb.Parity.Manifests;
using LoDb.Parity.Projections;

namespace LoDb.Parity.Runs;

/// <summary>
/// A run directory of tools/parity: <c>sample.json</c>,
/// <c>{legacy,next}/export/{version}/{language}.json</c>,
/// <c>legacy/manifest/{version}/{type}.json</c> and <c>next/assets.json</c>.
/// </summary>
public sealed class ParityRun
{
    private static readonly string[] ManifestTypes =
        ["champion", "item", "runesReforged", "summoner"];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string directory;
    private readonly ILookup<string, ManifestRow> assets;

    private ParityRun(string directory, RunSample sample)
    {
        this.directory = directory;
        Sample = sample;
        assets = Read<List<ManifestRow>>("next/assets.json").ToLookup(static row => row.Version);
    }

    public RunSample Sample { get; }

    public List<PairCoverage> Coverage { get; } = [];

    /// <summary>What the run compared, filled with <see cref="Coverage"/>.</summary>
    public RunTally Tally { get; } = new();

    public static ParityRun Open(string directory)
    {
        var sample = JsonSerializer.Deserialize<RunSample>(
            File.ReadAllText(Path.Combine(directory, "sample.json")),
            Options) ?? throw new InvalidDataException($"{directory}: empty sample.json");
        return new ParityRun(directory, sample);
    }

    /// <summary>
    /// Every deviation of the run, version by version; fills <see cref="Coverage"/> on the way.
    /// </summary>
    public IEnumerable<Deviation> Deviations()
    {
        foreach (var version in Sample.Versions)
        {
            var tags = new KeyTags();
            var placeholders = PlaceholdersOf(version);
            foreach (var language in Sample.Languages)
            {
                var pair = ProjectionsOf(version, language) with { Placeholders = placeholders };
                tags.Add(pair.Next);
                tags.AddLegacyDetails(pair.Legacy);
                Coverage.Add(CoverageOf(pair));
                Tally.Count(pair);
                foreach (var deviation in ProjectionComparer.Compare(pair))
                {
                    yield return deviation;
                }
            }

            foreach (var deviation in ManifestTypes.SelectMany(t => Manifests(version, t, tags)))
            {
                yield return deviation;
            }
        }
    }

    private static PairCoverage CoverageOf(ProjectionPair pair) => new()
    {
        Version = pair.Version,
        Language = pair.Language,
        Legacy = pair.Legacy["champions"]?["contentLanguage"]?.GetValue<string>(),
        Next = pair.Next["champions"]?["contentLanguage"]?.GetValue<string>(),
    };

    private ProjectionPair ProjectionsOf(string version, string language) => new()
    {
        Version = version,
        Language = language,
        Legacy = Read<JsonObject>($"legacy/export/{version}/{language}.json"),
        Next = Read<JsonObject>($"next/export/{version}/{language}.json"),
    };

    // Read from the new en_US export; none when the run left en_US out of its sample.
    private IReadOnlySet<string> PlaceholdersOf(string version)
    {
        var english = $"next/export/{version}/en_US.json";
        return File.Exists(Path.Combine(directory, english))
            ? EntryTags.PlaceholdersOf(Read<JsonObject>(english))
            : new HashSet<string>();
    }

    private IReadOnlyList<Deviation> Manifests(string version, string type, KeyTags tags)
    {
        var legacyFile = $"legacy/manifest/{version}/{type}.json";
        var legacy = File.Exists(Path.Combine(directory, legacyFile))
            ? Read<Dictionary<string, string?>>(legacyFile)
            : [];
        var pair = new ManifestPair
        {
            Version = version,
            Type = type,
            Legacy = legacy,
            Next = assets[version].Where(row => row.Type == type).ToDictionary(row => row.Key),
            Tags = tags.Of(type),
        };
        Tally.Count(pair);
        return ManifestComparer.Compare(pair);
    }

    private T Read<T>(string file)
    {
        var path = Path.Combine(directory, file);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The run lacks {file}: collect it again.", path);
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"{path} is empty.");
    }
}
