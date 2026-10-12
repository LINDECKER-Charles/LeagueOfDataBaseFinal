using LoDb.Desktop.Hosting;
using LoDb.Desktop.Tests.Updates.Signing;
using LoDb.Desktop.Updates;
using LoDb.Desktop.Updates.Engine;
using LoDb.Desktop.Updates.Engine.Signing;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoDb.Desktop.Tests.Updates;

/// <summary>
/// The engine in a process that Velopack did not start, as in development: no install, so
/// nothing to check, download or apply.
/// </summary>
/// <remarks>
/// An invalid local feed ends the same way, with no install: UpdateFeedsTests refuses the
/// feed itself.
/// </remarks>
public sealed class VelopackEngineTests
{
    [Fact]
    public async Task OutsideAVelopackAppThereIsNoInstallToCheck()
    {
        var engine = new VelopackEngine(
            new UpdateSettings { Mode = UpdateMode.Background },
            new DesktopOptions
            {
                Version = "1.0.0",
                Channel = "stable",
                ApiOrigin = new Uri("https://league-of-data-base.com"),
                ShellDirectory = "shell",
                DataDirectory = "data",
            },
            new SignedFeeds(new TestFeedKey().Verifier(), NullLogger<SignedFeeds>.Instance),
            NullLogger<VelopackEngine>.Instance);

        Assert.False(engine.IsInstalled);
        Assert.Null(engine.Pending);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.FindNewerAsync(TestContext.Current.CancellationToken));
    }
}
