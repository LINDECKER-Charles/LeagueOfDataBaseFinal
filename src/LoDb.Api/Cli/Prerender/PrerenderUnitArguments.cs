using System.Collections.Frozen;
using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Cli.Prerender;

/// <summary>The options of <c>prerender unit</c>, read from its arguments.</summary>
/// <remarks>
/// An option comes as <c>--name value</c> or <c>--name=value</c>. Any other
/// <c>--key=value</c> is a host setting (<c>--ConnectionStrings:LoDb=…</c>), which the host
/// reads and the parser skips.
/// </remarks>
internal sealed record PrerenderUnitArguments
{
    public const string Usage =
        "Usage: prerender unit --version <x.y.z> [--locale <code>]"
        + " [--type champions|items|runes|summoners] [--epoch <n>] [--wait <seconds>]";

    /// <summary>
    /// How long the command waits by default for the unit's catalog, which it may have just
    /// queued for ingestion: a few minutes are enough on a stack whose catalogs are stored.
    /// </summary>
    public static readonly TimeSpan DefaultWait = TimeSpan.FromMinutes(5);

    private const string Prefix = "--";
    private const char Assignment = '=';
    private const string VersionOption = "version";
    private const string LocaleOption = "locale";
    private const string TypeOption = "type";
    private const string EpochOption = "epoch";
    private const string WaitOption = "wait";

    private static readonly FrozenSet<string> Known = FrozenSet.Create(
        StringComparer.Ordinal,
        VersionOption,
        LocaleOption,
        TypeOption,
        EpochOption,
        WaitOption);

    /// <summary>The older patch whose unit is written.</summary>
    public required PatchVersion Version { get; init; }

    /// <summary>The one (version, locale) to render, and the type of page if only one.</summary>
    public required PassScope Scope { get; init; }

    /// <summary>The epoch written; the served one when not given.</summary>
    public int? Epoch { get; init; }

    /// <summary>How long the unit may wait for its catalog before the command gives up.</summary>
    public TimeSpan Wait { get; init; } = DefaultWait;

    /// <exception cref="FormatException">The arguments are not a valid use.</exception>
    public static PrerenderUnitArguments Parse(IReadOnlyList<string> arguments)
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

            var value = option.Value ?? NextValue(tokens, option.Name);
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
        if (Known.Contains(name))
        {
            return (name, assignment < 0 ? null : body[(assignment + 1)..]);
        }

        return assignment < 0 ? throw Invalid($"Unknown option '{token}'.") : null;
    }

    private static string NextValue(IEnumerator<string> tokens, string name) =>
        tokens.MoveNext() && !tokens.Current.StartsWith(Prefix, StringComparison.Ordinal)
            ? tokens.Current
            : throw Invalid($"--{name} needs a value.");

    private static PrerenderUnitArguments Build(Dictionary<string, string> options)
    {
        var version = options.TryGetValue(VersionOption, out var text)
            ? VersionOf(text)
            : throw Invalid("--version is required.");
        var locale = options.TryGetValue(LocaleOption, out var code)
            ? LocaleOf(code)
            : UiLocales.Fallback;
        return new PrerenderUnitArguments
        {
            Version = version,
            Scope = PassScope.Of(version) with
            {
                Locales = [locale],
                PageTypes = options.TryGetValue(TypeOption, out var type) ? [TypeOf(type)] : null,
            },
            Epoch = options.TryGetValue(EpochOption, out var epoch) ? EpochOf(epoch) : null,
            Wait = options.TryGetValue(WaitOption, out var wait) ? WaitOf(wait) : DefaultWait,
        };
    }

    private static PatchVersion VersionOf(string text) =>
        PatchVersion.TryParse(text, out var version)
            ? version
            : throw Invalid($"'{text}' is not a Data Dragon version.");

    private static UiLocale LocaleOf(string text) =>
        UiLocales.TryParse(text, out var locale)
            ? locale
            : throw Invalid($"'{text}' is not a locale of the site (en, fr, zh-hans…).");

    private static ResourceType TypeOf(string text) =>
        PageTypes.TryParse(text, out var type)
            ? type
            : throw Invalid($"'{text}' is not a type of page.");

    private static int EpochOf(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var epoch)
        && epoch > 0
            ? epoch
            : throw Invalid("--epoch takes a positive number: epoch 0 is kept for the probes.");

    private static TimeSpan WaitOf(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : throw Invalid("--wait takes a number of seconds, 0 or more.");

    private static FormatException Invalid(string message) => new(message);
}
