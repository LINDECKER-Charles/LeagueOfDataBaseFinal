using System.Collections.Frozen;

namespace LoDb.Api.Cli.Catalog;

/// <summary>The options of <c>catalog states build</c>, read from its arguments.</summary>
/// <remarks>
/// Both options are switches. Any other <c>--key=value</c> is a host setting
/// (<c>--ConnectionStrings:LoDb=…</c>), which the host reads and the parser skips.
/// </remarks>
internal sealed record CatalogStatesBuildArguments
{
    public const string Usage = "Usage: catalog states build [--rebuild] [--ingest-missing]";

    private const string Prefix = "--";
    private const char Assignment = '=';
    private const string RebuildOption = "rebuild";
    private const string IngestMissingOption = "ingest-missing";

    private static readonly FrozenSet<string> Known = FrozenSet.Create(
        StringComparer.Ordinal,
        RebuildOption,
        IngestMissingOption);

    /// <summary>Rebuilds the index into a new generation, whatever the current covers.</summary>
    public bool ShouldRebuild { get; private init; }

    /// <summary>Ingests the en_US datasets every listed version up to the latest lacks.</summary>
    public bool ShouldIngestMissing { get; private init; }

    /// <exception cref="FormatException">The arguments are not a valid use.</exception>
    public static CatalogStatesBuildArguments Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var switches = new HashSet<string>(StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            if (SwitchOf(argument) is { } name && !switches.Add(name))
            {
                throw Invalid($"--{name} is given twice.");
            }
        }

        return new CatalogStatesBuildArguments
        {
            ShouldRebuild = switches.Contains(RebuildOption),
            ShouldIngestMissing = switches.Contains(IngestMissingOption),
        };
    }

    // Null for a host setting.
    private static string? SwitchOf(string token)
    {
        if (!token.StartsWith(Prefix, StringComparison.Ordinal) || token.Length == Prefix.Length)
        {
            throw Invalid($"Unexpected argument '{token}'.");
        }

        var body = token[Prefix.Length..];
        var assignment = body.IndexOf(Assignment, StringComparison.Ordinal);
        var name = assignment < 0 ? body : body[..assignment];
        if (!Known.Contains(name))
        {
            return assignment < 0 ? throw Invalid($"Unknown option '{token}'.") : null;
        }

        return assignment < 0 ? name : throw Invalid($"--{name} takes no value.");
    }

    private static FormatException Invalid(string message) => new(message);
}
