namespace LoDb.Api.Cli.Analytics;

/// <summary>The options of <c>analytics import</c>, read from its arguments.</summary>
/// <remarks>
/// An option comes as <c>--name value</c> or <c>--name=value</c>. Any other
/// <c>--key=value</c> is a host setting (<c>--ConnectionStrings:LoDb=…</c>), which the host
/// reads and the parser skips.
/// </remarks>
internal sealed record AnalyticsImportArguments
{
    public const string Usage = "Usage: analytics import --source <directory> [--dry-run]";

    private const string Prefix = "--";
    private const char Assignment = '=';
    private const string SourceOption = "source";
    private const string DryRunOption = "dry-run";

    /// <summary>The directory of the legacy day files.</summary>
    public required string Source { get; init; }

    /// <summary>Reads and counts, writes nothing.</summary>
    public bool DryRun { get; init; }

    /// <exception cref="FormatException">The arguments are not a valid use.</exception>
    public static AnalyticsImportArguments Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string? source = null;
        var dryRun = false;
        using var tokens = arguments.GetEnumerator();
        while (tokens.MoveNext())
        {
            var (name, value) = OptionOf(tokens.Current);
            switch (name)
            {
                case SourceOption when source is null:
                    source = value ?? NextValue(tokens);
                    break;
                case SourceOption:
                    throw Invalid("--source is given once.");
                case DryRunOption when value is null:
                    dryRun = true;
                    break;
                case DryRunOption:
                    throw Invalid("--dry-run takes no value.");
            }
        }

        return string.IsNullOrEmpty(source)
            ? throw Invalid("--source needs a directory.")
            : new AnalyticsImportArguments { Source = source, DryRun = dryRun };
    }

    // A null name for a host setting.
    private static (string? Name, string? Value) OptionOf(string token)
    {
        if (!token.StartsWith(Prefix, StringComparison.Ordinal) || token.Length == Prefix.Length)
        {
            throw Invalid($"Unexpected argument '{token}'.");
        }

        var body = token[Prefix.Length..];
        var assignment = body.IndexOf(Assignment, StringComparison.Ordinal);
        var name = assignment < 0 ? body : body[..assignment];
        var value = assignment < 0 ? null : body[(assignment + 1)..];
        if (name is SourceOption or DryRunOption)
        {
            return (name, value);
        }

        return assignment < 0 ? throw Invalid($"Unknown option '{token}'.") : (null, null);
    }

    private static string NextValue(IEnumerator<string> tokens) =>
        tokens.MoveNext() && !tokens.Current.StartsWith(Prefix, StringComparison.Ordinal)
            ? tokens.Current
            : throw Invalid("--source needs a directory.");

    private static FormatException Invalid(string message) => new(message);
}
