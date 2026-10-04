using LoDb.Api.Modules.ClientPolicy;
using LoDb.Api.Modules.ClientPolicy.Publishing;

namespace LoDb.Api.Cli.ClientPolicy;

/// <summary>The options of <c>client-policy publish</c>, read from its arguments.</summary>
/// <remarks>
/// An option comes as <c>--name value</c> or <c>--name=value</c>. A <c>--key=value</c> whose
/// key holds a colon is a host setting (<c>--ConnectionStrings:LoDb=…</c>), which the host
/// reads and the parser skips. Any other unknown option is refused: the publication replaces
/// the whole policy, so a misspelt <c>--minimum</c> would silently clear the floor.
/// </remarks>
internal sealed record ClientPolicyPublishArguments
{
    public const string Usage =
        "Usage: client-policy publish --platform desktop|android [--minimum <x.y.z>]"
        + " [--latest <x.y.z>] [--bundle-id <id> --bundle-url <https url>"
        + " --bundle-checksum <sha-256 hex> --bundle-signature <base64>"
        + " --bundle-minimum-native <x.y.z>]";

    private const string Prefix = "--";
    private const char Assignment = '=';
    private const char SettingSection = ':';
    private const string PlatformOption = "platform";
    private const string MinimumOption = "minimum";
    private const string LatestOption = "latest";
    private const string BundlePrefix = "bundle-";
    private const string BundleIdOption = "bundle-id";
    private const string BundleUrlOption = "bundle-url";
    private const string BundleChecksumOption = "bundle-checksum";
    private const string BundleSignatureOption = "bundle-signature";
    private const string BundleNativeOption = "bundle-minimum-native";

    private static readonly string[] BundleOptions =
    [
        BundleIdOption,
        BundleUrlOption,
        BundleChecksumOption,
        BundleSignatureOption,
        BundleNativeOption,
    ];

    public required ClientPlatform Platform { get; init; }

    public required PublishPolicyRequest Request { get; init; }

    /// <exception cref="FormatException">The arguments are not a valid use.</exception>
    public static ClientPolicyPublishArguments Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var values = ReadOptions(arguments);
        if (!ClientPlatforms.TryParse(values.GetValueOrDefault(PlatformOption), out var platform))
        {
            throw Invalid("--platform must be desktop or android.");
        }

        return new ClientPolicyPublishArguments
        {
            Platform = platform,
            Request = new PublishPolicyRequest
            {
                MinimumVersion = values.GetValueOrDefault(MinimumOption),
                LatestVersion = values.GetValueOrDefault(LatestOption),
                Bundle = BundleOf(values),
            },
        };
    }

    private static Dictionary<string, string> ReadOptions(IReadOnlyList<string> arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using var tokens = arguments.GetEnumerator();
        while (tokens.MoveNext())
        {
            var (name, value) = OptionOf(tokens.Current);
            if (name is null)
            {
                continue;
            }

            if (!values.TryAdd(name, value ?? NextValue(tokens, name)))
            {
                throw Invalid($"--{name} is given twice.");
            }
        }

        return values;
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
        if (IsOption(name))
        {
            return (name, value);
        }

        return assignment >= 0 && name.Contains(SettingSection, StringComparison.Ordinal)
            ? (null, null)
            : throw Invalid($"Unknown option '{token}'.");
    }

    private static bool IsOption(string name) =>
        name is PlatformOption or MinimumOption or LatestOption || BundleOptions.Contains(name);

    private static string NextValue(IEnumerator<string> tokens, string name) =>
        tokens.MoveNext() && !tokens.Current.StartsWith(Prefix, StringComparison.Ordinal)
            ? tokens.Current
            : throw Invalid($"--{name} needs a value.");

    // Any bundle option makes a bundle, whose missing fields the rules then name.
    private static BundleRequest? BundleOf(Dictionary<string, string> values) =>
        values.Keys.Any(static key => key.StartsWith(BundlePrefix, StringComparison.Ordinal))
            ? new BundleRequest
            {
                Id = values.GetValueOrDefault(BundleIdOption),
                Url = values.GetValueOrDefault(BundleUrlOption),
                Checksum = values.GetValueOrDefault(BundleChecksumOption),
                Signature = values.GetValueOrDefault(BundleSignatureOption),
                MinimumNativeVersion = values.GetValueOrDefault(BundleNativeOption),
            }
            : null;

    private static FormatException Invalid(string message) => new(message);
}
