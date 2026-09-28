using System.Reflection;

namespace LoDb.Api.Hosting.Telemetry;

/// <summary>
/// Version of the API assembly, without the source revision the SDK appends after "+".
/// </summary>
/// <remarks>
/// The revision is reported on its own label, from <c>APP_REVISION</c>.
/// </remarks>
internal static class BuildVersion
{
    private const char MetadataSeparator = '+';
    private const string Fallback = "0.0.0";

    public static string Current { get; } = Read();

    private static string Read()
    {
        var version = typeof(BuildVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? Fallback;
        var separator = version.IndexOf(MetadataSeparator, StringComparison.Ordinal);
        return separator < 0 ? version : version[..separator];
    }
}
