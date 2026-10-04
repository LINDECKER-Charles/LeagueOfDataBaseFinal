using LoDb.Api.Modules.Admin.Storage.Views;

namespace LoDb.Api.Modules.Admin.Storage;

/// <summary>The languages and dataset types stored for each version.</summary>
internal sealed class DatasetCoverage
{
    private const char VersionSeparator = '.';

    private readonly Dictionary<string, Datasets> _versions = new(StringComparer.Ordinal);

    public void Add(string version, string lang, string type)
    {
        if (!_versions.TryGetValue(version, out var coverage))
        {
            coverage = new Datasets();
            _versions[version] = coverage;
        }

        coverage.Langs.Add(lang);
        coverage.Types.Add(type);
        coverage.Objects++;
    }

    /// <summary>The versions, the latest first by their numbers.</summary>
    public IReadOnlyList<VersionCoverage> Rows() =>
        [
            .. _versions
                .OrderByDescending(static version => SortKey(version.Key))
                .ThenByDescending(static version => version.Key, StringComparer.Ordinal)
                .Select(static version => new VersionCoverage
                {
                    Version = version.Key,
                    Langs = [.. version.Value.Langs],
                    Types = [.. version.Value.Types],
                    Objects = version.Value.Objects,
                }),
        ];

    // "15.18.1" sorts after "15.9.1": each part compared as a number, missing ones as 0.
    private static Version SortKey(string version)
    {
        var parts = version.Split(VersionSeparator)
            .Select(static part => int.TryParse(part, out var number) ? number : 0)
            .Concat(Enumerable.Repeat(0, 3))
            .Take(3)
            .ToArray();
        return new Version(Math.Max(0, parts[0]), Math.Max(0, parts[1]), Math.Max(0, parts[2]));
    }

    private sealed class Datasets
    {
        public SortedSet<string> Langs { get; } = new(StringComparer.Ordinal);

        public SortedSet<string> Types { get; } = new(StringComparer.Ordinal);

        public long Objects { get; set; }
    }
}
