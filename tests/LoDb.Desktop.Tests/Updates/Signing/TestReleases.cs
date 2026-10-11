using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoDb.Desktop.Tests.Updates.Signing;

/// <summary>
/// Release feeds and packages of the win-x64 channel, as vpk writes them: a package is a few
/// bytes, which only its size and SHA-256 describe.
/// </summary>
internal static class TestReleases
{
    public const string Channel = "win-x64";
    public const string FeedName = "releases.win-x64.json";

    public static string FileName(string version) => $"LoDb.Desktop-{version}-win-x64-full.nupkg";

    public static byte[] Package(string version) => Encoding.UTF8.GetBytes($"package {version}");

    public static byte[] Feed(params string[] versions) =>
        FeedOf(versions.Select(version => Entry(version, FileName(version))));

    public static byte[] FeedOf(IEnumerable<Dictionary<string, object?>> entries) =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["Assets"] = entries.ToArray(),
        });

    /// <summary>The entry of a full package, with the size and SHA-256 of its bytes.</summary>
    public static Dictionary<string, object?> Entry(string version, string fileName)
    {
        var package = Package(version);
        return new Dictionary<string, object?>
        {
            ["PackageId"] = "LoDb.Desktop",
            ["Version"] = version,
            ["Type"] = "Full",
            ["FileName"] = fileName,
            ["SHA256"] = Convert.ToHexString(SHA256.HashData(package)),
            ["Size"] = package.Length,
        };
    }
}
