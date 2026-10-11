using System.Text.Json;
using Velopack.Sources;

namespace LoDb.Desktop.Tests.Updates.Signing;

/// <summary>
/// The GitHub Releases of the repository in memory, as Velopack's downloader sees them: the
/// releases API lists what <see cref="Publish"/> added, newest first, and each asset is served
/// at its browser URL.
/// </summary>
internal sealed class FakeGithub : IFileDownloader
{
    private static readonly DateTime FirstRelease = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly List<object> _releases = [];
    private readonly Dictionary<string, byte[]> _assets = new(StringComparer.Ordinal);

    public List<string> Downloads { get; } = [];

    public void Publish(string release, IReadOnlyDictionary<string, byte[]> files)
    {
        foreach (var (name, content) in files)
        {
            _assets[UrlOf(release, name)] = content;
        }

        _releases.Insert(0, new Dictionary<string, object>
        {
            ["name"] = release,
            ["prerelease"] = false,
            ["published_at"] = FirstRelease.AddDays(_releases.Count),
            ["assets"] = files.Keys.Select(name => new Dictionary<string, string>
            {
                ["name"] = name,
                ["browser_download_url"] = UrlOf(release, name),
            }).ToArray(),
        });
    }

    public Task<string> DownloadString(
        string url,
        IDictionary<string, string>? headers = null,
        double timeout = 30) =>
        Task.FromResult(JsonSerializer.Serialize(_releases));

    public Task<byte[]> DownloadBytes(
        string url,
        IDictionary<string, string>? headers = null,
        double timeout = 30)
    {
        Downloads.Add(url);
        return Task.FromResult(_assets[url]);
    }

    public async Task DownloadFile(
        string url,
        string targetFile,
        Action<int> progress,
        IDictionary<string, string>? headers = null,
        double timeout = 30,
        CancellationToken cancelToken = default)
    {
        Downloads.Add(url);
        await File.WriteAllBytesAsync(targetFile, _assets[url], cancelToken);
    }

    private static string UrlOf(string release, string file) =>
        $"https://github.com/owner/repo/releases/download/{release}/{file}";
}
