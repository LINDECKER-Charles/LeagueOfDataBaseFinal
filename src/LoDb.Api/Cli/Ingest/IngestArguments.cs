using System.Collections.Frozen;
using System.Globalization;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Cli.Ingest;

/// <summary>The options of <c>ingest</c>, read from its arguments.</summary>
/// <remarks>
/// An option comes as <c>--name value</c> or <c>--name=value</c>. Any other
/// <c>--key=value</c> is a host setting (<c>--ConnectionStrings:LoDb=…</c>), which the host
/// reads and the parser skips.
/// </remarks>
internal sealed record IngestArguments
{
    public const string Usage =
        "Usage: ingest (--version <x.y.z> | --latest <1-50>) [--languages all|<code>[,<code>]]"
        + " [--force]";

    private const int MaxLatest = 50;
    private const string Prefix = "--";
    private const char Assignment = '=';
    private const char ListSeparator = ',';
    private const string VersionOption = "version";
    private const string LatestOption = "latest";
    private const string LanguagesOption = "languages";
    private const string ForceOption = "force";
    private const string AllLanguages = "all";

    private static readonly FrozenSet<string> Known = FrozenSet.Create(
        StringComparer.Ordinal,
        VersionOption,
        LatestOption,
        LanguagesOption,
        ForceOption);

    /// <summary>The version named, or <see langword="null"/> with <see cref="Latest"/>.</summary>
    public PatchVersion? Version { get; private init; }

    /// <summary>How many of the newest versions, when no version is named.</summary>
    public int Latest { get; private init; }

    /// <summary>The languages, <see langword="null"/> for every one Data Dragon lists.</summary>
    public IReadOnlyList<DdragonLanguage>? Languages { get; private init; }

    public bool Force { get; private init; }

    /// <exception cref="FormatException">The arguments are not a valid use.</exception>
    public static IngestArguments Parse(IReadOnlyList<string> arguments)
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

            var value = option.Value
                ?? (option.Name == ForceOption ? string.Empty : NextValue(tokens, option.Name));
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

        return name == ForceOption && value is not null
            ? throw Invalid("--force takes no value.")
            : (name, value);
    }

    private static string NextValue(IEnumerator<string> tokens, string name) =>
        tokens.MoveNext() && !tokens.Current.StartsWith(Prefix, StringComparison.Ordinal)
            ? tokens.Current
            : throw Invalid($"--{name} needs a value.");

    private static IngestArguments Build(Dictionary<string, string> options)
    {
        var version = options.GetValueOrDefault(VersionOption);
        var latest = options.GetValueOrDefault(LatestOption);
        if ((version is null) == (latest is null))
        {
            throw Invalid("Give either --version or --latest.");
        }

        return new IngestArguments
        {
            Version = version is null ? null : VersionOf(version),
            Latest = latest is null ? 0 : CountOf(latest),
            Languages = LanguagesOf(options.GetValueOrDefault(LanguagesOption) ?? AllLanguages),
            Force = options.ContainsKey(ForceOption),
        };
    }

    private static PatchVersion VersionOf(string text) =>
        PatchVersion.TryParse(text, out var version)
            ? version
            : throw Invalid($"'{text}' is not a Data Dragon version.");

    private static int CountOf(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
        && count is > 0 and <= MaxLatest
            ? count
            : throw Invalid("--latest takes a count from 1 to 50.");

    private static List<DdragonLanguage>? LanguagesOf(string text)
    {
        if (text == AllLanguages)
        {
            return null;
        }

        var codes = text.Split(
            ListSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return codes.Length > 0
            ? [.. codes.Select(LanguageOf).Distinct()]
            : throw Invalid("--languages needs 'all' or language codes.");
    }

    private static DdragonLanguage LanguageOf(string code) =>
        DdragonLanguage.TryParse(code, out var language)
            ? language
            : throw Invalid($"'{code}' is not a Data Dragon language code.");

    private static FormatException Invalid(string message) => new(message);
}
