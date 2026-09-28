using System.Collections.Frozen;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Cli.Catalog;

/// <summary>The options of <c>catalog export</c>, read from its arguments.</summary>
/// <remarks>
/// An option comes as <c>--name value</c> or <c>--name=value</c>. Any other
/// <c>--key=value</c> is a host setting (<c>--ConnectionStrings:LoDb=…</c>), which the host
/// reads and the parser skips.
/// </remarks>
internal sealed record CatalogExportArguments
{
    public const string Usage =
        "Usage: catalog export --version <x.y.z> --lang <code> [--output <file>]"
        + " [--stored-only]";

    private const string Prefix = "--";
    private const char Assignment = '=';
    private const string VersionOption = "version";
    private const string LanguageOption = "lang";
    private const string OutputOption = "output";
    private const string StoredOnlyOption = "stored-only";

    private static readonly FrozenSet<string> Known = FrozenSet.Create(
        StringComparer.Ordinal,
        VersionOption,
        LanguageOption,
        OutputOption,
        StoredOnlyOption);

    public required PatchVersion Version { get; init; }

    public required DdragonLanguage Language { get; init; }

    /// <summary>The file written; <see langword="null"/> for the standard output.</summary>
    public string? Output { get; init; }

    /// <summary>Reads the store alone: nothing is ingested.</summary>
    public bool StoredOnly { get; init; }

    /// <exception cref="FormatException">The arguments are not a valid use.</exception>
    public static CatalogExportArguments Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        using var tokens = arguments.GetEnumerator();
        while (tokens.MoveNext())
        {
            if (OptionOf(tokens.Current) is not { } option)
            {
                continue;
            }

            var value = option.Value ?? (option.Name == StoredOnlyOption
                ? string.Empty
                : NextValue(tokens, option.Name));
            if (!options.TryAdd(option.Name, value))
            {
                throw Invalid($"--{option.Name} is given twice.");
            }
        }

        return Build(options);
    }

    // Null for a host setting.
    private static (string Name, string? Value)? OptionOf(string token)
    {
        if (!token.StartsWith(Prefix, StringComparison.Ordinal) || token.Length == Prefix.Length)
        {
            throw Invalid($"Unexpected argument '{token}'.");
        }

        var body = token[Prefix.Length..];
        var assignment = body.IndexOf(Assignment, StringComparison.Ordinal);
        var name = assignment < 0 ? body : body[..assignment];
        var value = assignment < 0 ? null : body[(assignment + 1)..];
        if (!Known.Contains(name))
        {
            return assignment < 0 ? throw Invalid($"Unknown option '{token}'.") : null;
        }

        return name == StoredOnlyOption && value is not null
            ? throw Invalid("--stored-only takes no value.")
            : (name, value);
    }

    private static string NextValue(IEnumerator<string> tokens, string name) =>
        tokens.MoveNext() && !tokens.Current.StartsWith(Prefix, StringComparison.Ordinal)
            ? tokens.Current
            : throw Invalid($"--{name} needs a value.");

    private static CatalogExportArguments Build(Dictionary<string, string> options)
    {
        var version = options.GetValueOrDefault(VersionOption)
            ?? throw Invalid("--version is required.");
        var language = options.GetValueOrDefault(LanguageOption)
            ?? throw Invalid("--lang is required.");
        var output = options.GetValueOrDefault(OutputOption);
        return new CatalogExportArguments
        {
            Version = PatchVersion.TryParse(version, out var parsed)
                ? parsed
                : throw Invalid($"'{version}' is not a Data Dragon version."),
            Language = DdragonLanguage.TryParse(language, out var code)
                ? code
                : throw Invalid($"'{language}' is not a Data Dragon language code."),
            Output = output is { Length: 0 } ? throw Invalid("--output needs a file.") : output,
            StoredOnly = options.ContainsKey(StoredOnlyOption),
        };
    }

    private static FormatException Invalid(string message) => new(message);
}
