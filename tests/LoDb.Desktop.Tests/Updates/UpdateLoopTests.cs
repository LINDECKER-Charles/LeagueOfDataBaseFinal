using LoDb.Desktop.Lifecycle;
using LoDb.Desktop.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Desktop.Tests.Updates;

public sealed class UpdateLoopTests : IDisposable
{
    private readonly FakeUpdateEngine _engine = new();
    private readonly FakeTimeProvider _time =
        new(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));
    private readonly VelopackUpdates _updates;
    private UpdateLoop? _loop;

    public UpdateLoopTests() => _updates = new VelopackUpdates(
        _engine,
        new FakeLifetime(),
        NullLogger<VelopackUpdates>.Instance);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _loop?.Dispose();

    [Fact]
    public async Task ChecksAtStartThenEverySixHours()
    {
        var loop = LoopIn(UpdateMode.Background);

        await loop.StartAsync(Token);
        await _engine.WaitForChecksAsync(1, Token);
        _time.Advance(UpdateLoop.CheckInterval);
        await _engine.WaitForChecksAsync(2, Token);
        _time.Advance(UpdateLoop.CheckInterval);
        await _engine.WaitForChecksAsync(3, Token);
        await loop.StopAsync(Token);

        Assert.Equal(TimeSpan.FromHours(6), UpdateLoop.CheckInterval);
    }

    [Fact]
    public async Task NeverChecksForAPlainSmokeCheck()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");
        var loop = LoopIn(UpdateMode.Off);

        await loop.StartAsync(Token);
        await loop.StopAsync(Token);

        Assert.Equal(0, _engine.CheckCount);
        Assert.Empty(_engine.HandOvers);
    }

    [Fact]
    public async Task AwaitsTheDownloadAtStartThenHandsItOverAtStop()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");
        var loop = LoopIn(UpdateMode.ApplyAtExit);

        await loop.StartAsync(Token);
        var afterStart = _updates.Current;
        await loop.StopAsync(Token);

        Assert.Equal(UpdateStage.Ready, afterStart.Stage);
        Assert.Equal(["at-exit 2.0.0"], _engine.HandOvers);
    }

    [Fact]
    public async Task StartsAnywayWhenTheAwaitedCheckTimesOut()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");
        _engine.DownloadGate = new TaskCompletionSource();
        var loop = LoopIn(UpdateMode.ApplyAtExit);

        var start = loop.StartAsync(Token);
        await _engine.WaitForChecksAsync(1, Token);
        _time.Advance(UpdateLoop.AwaitedCheckTimeout);
        await start.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await loop.StopAsync(Token);

        Assert.Equal(UpdateSnapshot.None, _updates.Current);
        Assert.Empty(_engine.HandOvers);
    }

    [Fact]
    public async Task HandsAReadyUpdateOverWhenTheHostStops()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");
        var loop = LoopIn(UpdateMode.Background);

        await loop.StartAsync(Token);
        await _engine.WaitForChecksAsync(1, Token);
        await WaitForReadyAsync();
        await loop.StopAsync(Token);

        Assert.Equal(["at-exit 2.0.0"], _engine.HandOvers);
    }

    private UpdateLoop LoopIn(UpdateMode mode)
    {
        _loop = new UpdateLoop(
            _updates,
            new UpdateSettings { Mode = mode },
            _time,
            NullLogger<UpdateLoop>.Instance);
        return _loop;
    }

    // The check runs on the loop's own task: its download ends shortly after it starts.
    private async Task WaitForReadyAsync()
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bounded.CancelAfter(TimeSpan.FromSeconds(10));
        while (_updates.Current.Stage != UpdateStage.Ready)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), bounded.Token);
        }
    }
}
