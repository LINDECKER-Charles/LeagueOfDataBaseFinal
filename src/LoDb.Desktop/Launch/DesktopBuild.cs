using System.Reflection;

namespace LoDb.Desktop.Launch;

/// <summary>What a build baked into the assembly: version, channel and Google client.</summary>
internal sealed record DesktopBuild
{
    /// <summary>Metadata key of the release channel, set by the project file.</summary>
    public const string ChannelKey = "LoDbDesktopChannel";

    /// <summary>Metadata key of the "desktop app" Google client, set by the project file.</summary>
    public const string GoogleClientKey = "LoDbDesktopGoogleClientId";

    private const char SourceRevisionSeparator = '+';

    public required string Version { get; init; }

    public required string Channel { get; init; }

    public string? GoogleClientId { get; init; }

    public static DesktopBuild Of(Assembly assembly) => new()
    {
        Version = ReadVersion(assembly),
        Channel = ReadMetadata(assembly, ChannelKey) ?? DesktopChannels.Stable,
        GoogleClientId = ReadMetadata(assembly, GoogleClientKey),
    };

    // The SDK appends "+{commit}" to the informational version; the front, the API and the
    // update feed compare release versions only.
    private static string ReadVersion(Assembly assembly)
    {
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? string.Empty;
        var separator = version.IndexOf(SourceRevisionSeparator, StringComparison.Ordinal);
        return separator < 0 ? version : version[..separator];
    }

    private static string? ReadMetadata(Assembly assembly, string key)
    {
        var value = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key.Equals(key, StringComparison.Ordinal))?
            .Value;
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
