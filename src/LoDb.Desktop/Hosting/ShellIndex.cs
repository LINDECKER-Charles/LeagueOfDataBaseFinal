using System.Text.Json;

namespace LoDb.Desktop.Hosting;

/// <summary>
/// The shell's <c>index.html</c> with the desktop marker (plan §5.2):
/// <c>window.__LODB_DESKTOP__ = {version, bridge?}</c>, run before any other script.
/// </summary>
internal sealed class ShellIndex(DesktopOptions options, InjectedBridge bridge)
{
    public const string FileName = "index.html";

    private const string HeadStart = "<head";
    private const char TagEnd = '>';

    // Read once: the shell does not change under a running app.
    private readonly Lazy<string?> _template = new(() => Read(options.ShellDirectory));

    public string FilePath => Path.Combine(options.ShellDirectory, FileName);

    public bool IsAvailable => _template.Value is not null;

    /// <summary>The page to serve, or null when the shell build is missing.</summary>
    public string? Render() =>
        _template.Value is { } template
            ? Inject(template, MarkerScript(options.Version, bridge.Transport))
            : null;

    /// <summary>
    /// The marker script. The version goes through the JSON encoder, which escapes
    /// <c>&lt;</c> and quotes, so no value can close the script element.
    /// </summary>
    public static string MarkerScript(string version, string? transport)
    {
        var bridgeMember = transport is null ? string.Empty : $",bridge:{transport}";
        var versionLiteral = JsonSerializer.Serialize(version);
        return "<script>window.__LODB_DESKTOP__=Object.freeze("
            + $"{{version:{versionLiteral}{bridgeMember}}});</script>";
    }

    private static string Inject(string template, string script)
    {
        var head = template.IndexOf(HeadStart, StringComparison.OrdinalIgnoreCase);
        var headEnd = head < 0 ? -1 : template.IndexOf(TagEnd, head);
        return headEnd < 0 ? script + template : template.Insert(headEnd + 1, script);
    }

    private static string? Read(string directory)
    {
        try
        {
            return File.ReadAllText(Path.Combine(directory, FileName));
        }
        catch (Exception exception) when (exception is FileNotFoundException
            or DirectoryNotFoundException)
        {
            return null;
        }
    }
}
