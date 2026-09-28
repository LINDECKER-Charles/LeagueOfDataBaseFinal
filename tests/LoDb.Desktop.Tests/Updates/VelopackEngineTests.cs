using LoDb.Desktop.Hosting;
using LoDb.Desktop.Updates;
using LoDb.Desktop.Updates.Engine;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoDb.Desktop.Tests.Updates;

/// <summary>
/// The engine in a process that Velopack did not start, as in development: no install, so
/// nothing to check, download or apply.
/// </summary>
public sealed class VelopackEngineTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("relative/releases")]
    public void ReportsNoInstallOutsideAVelopackApp(string? localFeed)
    {
        var engine = EngineWith(new UpdateSettings
        {
            Mode = UpdateMode.Background,
            LocalFeed = localFeed,
        });

        Assert.False(engine.IsInstalled);
        Assert.Null(engine.Pending);
    }

    [Fact]
    public async Task RefusesToCheckWithoutAnInstall()
    {
        var engine = EngineWith(new UpdateSettings { Mode = UpdateMode.Background });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.FindNewerAsync(TestContext.Current.CancellationToken));
    }

    private static VelopackEngine EngineWith(UpdateSettings settings) => new(
        settings,
        new DesktopOptions
        {
            Version = "1.0.0",
            Channel = "stable",
            ApiOrigin = new Uri("https://league-of-data-base.com"),
            ShellDirectory = "shell",
            DataDirectory = "data",
        },
        NullLogger<VelopackEngine>.Instance);
}
