using LoDb.Desktop.Hosting;

namespace LoDb.Desktop.Launch;

/// <summary>
/// Reads the command line and the environment into a launch. Unknown arguments are ignored:
/// Velopack and the OS may pass their own.
/// </summary>
internal static class DesktopArguments
{
    public const string Smoke = "--smoke";
    public const string ProbeApi = "--probe-api";
    public const string DevTools = "--devtools";
    public const string ApiOrigin = "--api-origin";
    public const string ShellDir = "--shell-dir";
    public const string DataDir = "--data-dir";
    public const string ApiOriginVariable = "LODB_DESKTOP_API_ORIGIN";
    public const string DataDirectoryVariable = "LODB_DESKTOP_DATA_DIR";
    public const string GoogleClientVariable = "LODB_DESKTOP_GOOGLE_CLIENT_ID";

    private const string ShellFolder = "shell";
    private const char ValueSeparator = '=';
    private static readonly string[] ValuedFlags = [ApiOrigin, ShellDir, DataDir];

    /// <exception cref="ArgumentException">
    /// A flag lacks its value, or the API origin is invalid.
    /// </exception>
    public static DesktopLaunch Parse(
        IReadOnlyList<string> args,
        Func<string, string?> environment,
        DesktopBuild build)
    {
        var flags = ReadFlags(args);
        string? Setting(string flag, string variable) =>
            flags.GetValueOrDefault(flag) ?? NullIfBlank(environment(variable));

        var origin = Setting(ApiOrigin, ApiOriginVariable);
        var options = new DesktopOptions
        {
            Version = build.Version,
            Channel = build.Channel,
            ApiOrigin = origin is null
                ? DesktopChannels.ApiOriginOf(build.Channel)
                : ApiOrigins.Parse(origin),
            ShellDirectory = flags.GetValueOrDefault(ShellDir)
                ?? Path.Combine(AppContext.BaseDirectory, ShellFolder),
            DataDirectory = Setting(DataDir, DataDirectoryVariable)
                ?? DataDirectory.DefaultFor(build.Channel),
            GoogleClientId = NullIfBlank(environment(GoogleClientVariable)) ?? build.GoogleClientId,
            IsDevToolsEnabled = flags.ContainsKey(DevTools),
        };
        return new DesktopLaunch
        {
            Mode = flags.ContainsKey(Smoke) ? LaunchMode.Smoke : LaunchMode.Window,
            Options = options,
            ShouldProbeApi = flags.ContainsKey(ProbeApi),
        };
    }

    // Accepts "--flag value" and "--flag=value"; switches map to null.
    private static Dictionary<string, string?> ReadFlags(IReadOnlyList<string> args)
    {
        var flags = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index++)
        {
            var (name, value) = Split(args[index]);
            if (ValuedFlags.Contains(name, StringComparer.Ordinal) && value is null)
            {
                value = index + 1 < args.Count
                    ? args[++index]
                    : throw new ArgumentException($"{name} needs a value.", nameof(args));
            }

            flags[name] = NullIfBlank(value);
        }

        return flags;
    }

    private static (string Name, string? Value) Split(string argument)
    {
        var separator = argument.IndexOf(ValueSeparator, StringComparison.Ordinal);
        return separator < 0
            ? (argument, null)
            : (argument[..separator], argument[(separator + 1)..]);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
