namespace LoDb.Api.Cli.Audit;

/// <summary>The options of <c>audit import</c>, read from its arguments.</summary>
/// <remarks>
/// An option comes as <c>--name value</c> or <c>--name=value</c>. Any other
/// <c>--key=value</c> is a host setting (<c>--ConnectionStrings:LoDb=…</c>), which the host
/// reads and the parser skips.
/// </remarks>
internal sealed record AuditImportArguments
{
    public const string Usage =
        "Usage: audit import --source <directory> [--source <directory>…] [--dry-run]";

    private const string Prefix = "--";
    private const char Assignment = '=';
    private const string SourceOption = "source";
    private const string DryRunOption = "dry-run";

    /// <summary>The directories of day files, in the order given: the first wins a day.</summary>
    public required IReadOnlyList<string> Sources { get; init; }

    /// <summary>Reads and counts, writes nothing.</summary>
    public bool DryRun { get; init; }

    /// <exception cref="FormatException">The arguments are not a valid use.</exception>
    public static AuditImportArguments Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var sources = new List<string>();
        var dryRun = false;
        using var tokens = arguments.GetEnumerator();
        while (tokens.MoveNext())
        {
            var (name, value) = OptionOf(tokens.Current);
            switch (name)
            {
                case SourceOption:
                    sources.Add(value ?? NextValue(tokens));
                    break;
                case DryRunOption when value is null:
                    dryRun = true;
                    break;
                case DryRunOption:
                    throw Invalid("--dry-run takes no value.");
            }
        }

        return sources.Count == 0 || sources.Exists(static source => source.Length == 0)
            ? throw Invalid("--source needs a directory.")
            : new AuditImportArguments { Sources = sources, DryRun = dryRun };
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
