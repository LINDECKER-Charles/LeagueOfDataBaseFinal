using System.Reflection;

namespace LoDb.Testing;

/// <summary>
/// The schema the Doctrine migrations create, frozen by <c>tools/next/schema/check.sh</c> in
/// <c>tests/fixtures/schema/</c>: the tests compare with it without the legacy stack.
/// </summary>
public static class LegacySchema
{
    /// <summary>The normalized <c>pg_dump --schema-only</c>, executable as it is.</summary>
    public static string DoctrineSchemaSql { get; } = ReadResource("doctrine-schema.sql");

    /// <summary>The lines of <see cref="DoctrineSchemaSql"/>.</summary>
    public static IReadOnlyList<string> DoctrineSchema { get; } =
        SchemaDump.Normalize(DoctrineSchemaSql);

    /// <summary>Rows of <c>doctrine_migration_versions</c>, sorted.</summary>
    public static IReadOnlyList<string> DoctrineVersions { get; } =
        ReadResource("doctrine-versions.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary><c>tools/next/db/anonymize.sql</c>.</summary>
    public static string AnonymizeSql { get; } = ReadResource("anonymize.sql");

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded resource {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
