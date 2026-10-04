namespace LoDb.Testing;

/// <summary>
/// The normalization of <c>pg_dump</c> shared with <c>tools/schema/check.sh</c>: the
/// header, the settings and the blank lines vary with the run and the version.
/// </summary>
public static class SchemaDump
{
    private static readonly string[] NoisePrefixes =
    [
        "--",
        "SET ",
        "SELECT pg_catalog.set_config",
        "\\restrict",
        "\\unrestrict",
    ];

    public static IReadOnlyList<string> Normalize(string dump) =>
        dump.Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .Where(static line => line.Length > 0 && !IsNoise(line))
            .ToArray();

    private static bool IsNoise(string line) =>
        NoisePrefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal));
}
