using LoDb.Desktop.Lifecycle;
using LoDb.Desktop.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoDb.Desktop.Tests.Updates;

public sealed class VelopackUpdatesTests
{
    private readonly FakeUpdateEngine _engine = new();
    private readonly FakeLifetime _lifetime = new();
    private readonly VelopackUpdates _updates;

    public VelopackUpdatesTests() =>
        _updates = new VelopackUpdates(_engine, _lifetime, NullLogger<VelopackUpdates>.Instance);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NeverReadsTheFeedOfABuildVelopackDidNotInstall()
    {
        _engine.IsInstalled = false;
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");

        await _updates.CheckAsync(Token);

        Assert.Equal(0, _engine.CheckCount);
        Assert.Equal(UpdateSnapshot.None, _updates.Current);
    }

    [Fact]
    public async Task StaysAtNoneWhenNothingIsNewer()
    {
        await _updates.CheckAsync(Token);

        Assert.Equal(1, _engine.CheckCount);
        Assert.Equal(UpdateStage.None, _updates.Current.Stage);
        Assert.Empty(_engine.Downloads);
    }

    [Fact]
    public async Task ShowsTheDownloadThenTheReadyVersion()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");
        _engine.DownloadGate = new TaskCompletionSource();

        var check = _updates.CheckAsync(Token);
        Assert.Equal(Snapshot(UpdateStage.Downloading, "2.0.0"), _updates.Current);
        _engine.DownloadGate.SetResult();
        await check;

        Assert.Equal(Snapshot(UpdateStage.Ready, "2.0.0"), _updates.Current);
        Assert.Equal(["2.0.0"], _engine.Downloads);
    }

    [Fact]
    public async Task DoesNotDownloadTheReadyVersionAgain()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");

        await _updates.CheckAsync(Token);
        await _updates.CheckAsync(Token);

        Assert.Equal(2, _engine.CheckCount);
        Assert.Single(_engine.Downloads);
    }

    [Fact]
    public async Task ReplacesTheReadyVersionWithANewerOne()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");
        await _updates.CheckAsync(Token);

        _engine.Newer = FakeUpdateEngine.Release("2.1.0");
        await _updates.CheckAsync(Token);

        Assert.Equal(Snapshot(UpdateStage.Ready, "2.1.0"), _updates.Current);
        Assert.Equal(["2.0.0", "2.1.0"], _engine.Downloads);
    }

    [Fact]
    public async Task AdoptsAReleaseAnEarlierRunDownloaded()
    {
        _engine.Pending = FakeUpdateEngine.Release("2.0.0");
        _engine.Newer = _engine.Pending;

        await _updates.CheckAsync(Token);

        Assert.Equal(Snapshot(UpdateStage.Ready, "2.0.0"), _updates.Current);
        Assert.Empty(_engine.Downloads);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task OutlivesAFeedItCannotRead(Type failure)
    {
        // An HTTP timeout is a TaskCanceledException that no caller asked for.
        _engine.CheckFailure = (Exception)Activator.CreateInstance(failure)!;

        await _updates.CheckAsync(Token);

        Assert.Equal(UpdateSnapshot.None, _updates.Current);
    }

    [Fact]
    public async Task GoesBackAfterAFailedDownloadAndRetriesAtTheNextCheck()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");
        _engine.DownloadFailure = new IOException("checksum mismatch");

        await _updates.CheckAsync(Token);
        Assert.Equal(UpdateSnapshot.None, _updates.Current);

        _engine.DownloadFailure = null;
        await _updates.CheckAsync(Token);
        Assert.Equal(Snapshot(UpdateStage.Ready, "2.0.0"), _updates.Current);
    }

    [Fact]
    public async Task StopsWhenItsCallerCancels()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");
        _engine.DownloadGate = new TaskCompletionSource();
        using var cancellation = new CancellationTokenSource();

        var check = _updates.CheckAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check);
        Assert.Equal(UpdateSnapshot.None, _updates.Current);
    }

    [Fact]
    public async Task RefusesToApplyWhenNothingIsReady()
    {
        Assert.False(await _updates.ApplyAsync(Token));

        Assert.Empty(_engine.HandOvers);
        Assert.Equal(0, _lifetime.StopCount);
    }

    [Fact]
    public async Task AppliesOnRequestWithARestartThenStopsTheApp()
    {
        await ReadyAsync("2.0.0");

        Assert.True(await _updates.ApplyAsync(Token));

        Assert.Equal(["restart 2.0.0"], _engine.HandOvers);
        Assert.Equal(1, _lifetime.StopCount);
    }

    [Fact]
    public async Task AppliesSilentlyAtExit()
    {
        await ReadyAsync("2.0.0");

        _updates.PrepareExit();

        Assert.Equal(["at-exit 2.0.0"], _engine.HandOvers);
        Assert.Equal(0, _lifetime.StopCount);
    }

    [Fact]
    public async Task HandsTheReleaseOverOnceWhateverAsksFirst()
    {
        await ReadyAsync("2.0.0");

        await _updates.ApplyAsync(Token);
        _updates.PrepareExit();
        _updates.PrepareExit();

        Assert.Equal(["restart 2.0.0"], _engine.HandOvers);
    }

    [Fact]
    public async Task StopsCheckingOnceTheReleaseIsHandedOver()
    {
        await ReadyAsync("2.0.0");
        _updates.PrepareExit();
        _engine.Newer = FakeUpdateEngine.Release("2.1.0");

        await _updates.CheckAsync(Token);

        Assert.Equal(1, _engine.CheckCount);
    }

    [Fact]
    public void DoesNothingAtExitWithoutAReadyRelease()
    {
        _updates.PrepareExit();

        Assert.Empty(_engine.HandOvers);
    }

    [Fact]
    public async Task NeverThrowsAtExitWhenTheUpdaterCannotStart()
    {
        await ReadyAsync("2.0.0");
        _engine.UpdaterFailure = new FileNotFoundException("UpdateMac");

        _updates.PrepareExit();

        Assert.Empty(_engine.HandOvers);
        Assert.False(await _updates.ApplyAsync(Token));
        Assert.Equal(0, _lifetime.StopCount);
    }

    [Fact]
    public async Task TriesTheHandOverAgainAfterAFailure()
    {
        await ReadyAsync("2.0.0");
        _engine.UpdaterFailure = new FileNotFoundException("UpdateMac");
        _updates.PrepareExit();

        _engine.UpdaterFailure = null;
        _updates.PrepareExit();

        Assert.Equal(["at-exit 2.0.0"], _engine.HandOvers);
    }

    private async Task ReadyAsync(string version)
    {
        _engine.Newer = FakeUpdateEngine.Release(version);
        await _updates.CheckAsync(Token);
        Assert.Equal(UpdateStage.Ready, _updates.Current.Stage);
    }

    private static UpdateSnapshot Snapshot(UpdateStage stage, string version) =>
        new() { Stage = stage, Version = version };
}
