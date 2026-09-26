using System.Data.Common;
using System.Reflection;

namespace LoDb.Infrastructure.Persistence.Baseline;

/// <summary>
/// A comparable description of a schema: one line per table, column, constraint, index,
/// sequence, trigger, function or type, produced by <c>schema-catalog.sql</c>.
/// </summary>
/// <remarks>
/// <c>doctrine-catalog.txt</c> is that description for a database the Doctrine migrations
/// created, written by <c>tools/next/schema/</c> with the same query.
/// </remarks>
internal static class SchemaCatalog
{
    private const string QueryResource = "schema-catalog.sql";
    private const string DoctrineResource = "doctrine-catalog.txt";

    private static readonly Lazy<string> QueryText = new(() => ReadResource(QueryResource));

    private static readonly Lazy<IReadOnlyList<string>> DoctrineLines = new(() =>
        ReadResource(DoctrineResource)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>The query that describes the current schema.</summary>
    public static string Query => QueryText.Value;

    /// <summary>The catalog of the Doctrine schema, the only one the baseline accepts.</summary>
    public static IReadOnlyList<string> Doctrine => DoctrineLines.Value;

    /// <summary>The first column of every row of <paramref name="sql"/>, as text.</summary>
    public static async Task<IReadOnlyList<string>> ReadAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(reader.GetString(0));
        }

        return lines;
    }

    /// <summary>
    /// Lines missing from <paramref name="actual"/> (<c>-</c>), then lines it has in excess
    /// (<c>+</c>); empty when both describe the same schema.
    /// </summary>
    public static IReadOnlyList<string> Compare(
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var actualSet = actual.ToHashSet(StringComparer.Ordinal);
        var missing = expectedSet.Except(actualSet).Order(StringComparer.Ordinal);
        var unexpected = actualSet.Except(expectedSet).Order(StringComparer.Ordinal);
        return
        [
            .. missing.Select(static line => "- " + line),
            .. unexpected.Select(static line => "+ " + line),
        ];
    }

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded resource {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
