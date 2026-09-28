using System.Collections.Concurrent;
using LoDb.Desktop.Updates.Engine;
using Velopack;

namespace LoDb.Desktop.Tests.Updates;

/// <summary>
/// A feed and an updater in memory: the test sets the newest release and the failures, then
/// reads what was downloaded and handed over.
/// </summary>
internal sealed class FakeUpdateEngine : IUpdateEngine
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan WaitBound = TimeSpan.FromSeconds(10);

    private int _checkCount;

    public bool IsInstalled { get; set; } = true;

    public VelopackAsset? Pending { get; set; }

    /// <summary>The newest release of the feed; null when nothing is newer.</summary>
    public VelopackAsset? Newer { get; set; }

    public int DeltaCount { get; set; }

    public Exception? CheckFailure { get; set; }

    public Exception? DownloadFailure { get; set; }

    public Exception? UpdaterFailure { get; set; }

    /// <summary>Holds each download until the test completes it; null downloads at once.</summary>
    public TaskCompletionSource? DownloadGate { get; set; }

    public int CheckCount => Volatile.Read(ref _checkCount);

    public ConcurrentQueue<string> Downloads { get; } = new();

    /// <summary>Each start of the updater: <c>at-exit 1.2.0</c> or <c>restart 1.2.0</c>.</summary>
    public ConcurrentQueue<string> HandOvers { get; } = new();

    public static VelopackAsset Release(string version) => new()
    {
        PackageId = "LoDb.Desktop",
        Version = SemanticVersion.Parse(version),
        Type = VelopackAssetType.Full,
        FileName = $"LoDb.Desktop-{version}-osx-arm64-full.nupkg",
    };

    public Task<UpdateInfo?> FindNewerAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _checkCount);
        if (CheckFailure is not null)
        {
            return Task.FromException<UpdateInfo?>(CheckFailure);
        }

        return Task.FromResult(Newer is null ? null : UpdateTo(Newer));
    }

    public async Task DownloadAsync(UpdateInfo update, CancellationToken cancellationToken)
    {
        if (DownloadGate is not null)
        {
            await DownloadGate.Task.WaitAsync(cancellationToken);
        }

        if (DownloadFailure is not null)
        {
            throw DownloadFailure;
        }

        Downloads.Enqueue(update.TargetFullRelease.Version.ToString());
    }

    public void ApplyAtExit(VelopackAsset release) => StartUpdater("at-exit", release);

    public void RestartInto(VelopackAsset release) => StartUpdater("restart", release);

    /// <summary>Waits for the given number of checks in all, bounded so a hang shows.</summary>
    public async Task WaitForChecksAsync(int count, CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(WaitBound);
        while (CheckCount < count)
        {
            await Task.Delay(PollInterval, bounded.Token);
        }
    }

    private UpdateInfo UpdateTo(VelopackAsset release)
    {
        var deltas = Enumerable.Range(0, DeltaCount)
            .Select(_ => release with { Type = VelopackAssetType.Delta })
            .ToArray();
        return new UpdateInfo(release, isDowngrade: false, Pending, deltas);
    }

    private void StartUpdater(string kind, VelopackAsset release)
    {
        if (UpdaterFailure is not null)
        {
            throw UpdaterFailure;
        }

        HandOvers.Enqueue($"{kind} {release.Version}");
    }
}
