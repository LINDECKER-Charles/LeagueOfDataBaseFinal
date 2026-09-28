using LoDb.Desktop.Launch;
using LoDb.Desktop.Tests.Support;

namespace LoDb.Desktop.Tests.Smoke;

public sealed class SmokeTests : IDisposable
{
    private readonly TestFolder _folder = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _folder.Dispose();
        _output.Dispose();
        _error.Dispose();
    }

    private static string Version => DesktopBuild.Of(typeof(DesktopEntry).Assembly).Version;

    [Fact]
    public void PrintsTheVersionAndTheProxyThenExitsHealthy()
    {
        var exitCode = Run("--smoke", "--shell-dir", TestShell.CreateIn(_folder));
        var lines = Lines();

        Assert.Equal(0, exitCode);
        Assert.Equal($"LoDb.Desktop {Version} (channel stable)", lines[0]);
        Assert.Matches(@"^host: ok http://127\.0\.0\.1:\d+/$", lines[1]);
        Assert.Equal("proxy: ready /api/** -> https://league-of-data-base.com", lines[2]);
        Assert.StartsWith("shell: ok ", lines[3], StringComparison.Ordinal);
        Assert.Equal("updates: none", lines[4]);
        Assert.Equal("api: skipped (--probe-api to call it)", lines[5]);
        Assert.Equal("smoke: ok", lines[^1]);
    }

    [Fact]
    public void LeavesNothingInTheDataFolder()
    {
        Run("--smoke", "--shell-dir", TestShell.CreateIn(_folder));

        Assert.False(Directory.Exists(_folder.Combine("data")));
    }

    [Fact]
    public void FailsWithoutTheShellBuild()
    {
        var exitCode = Run("--smoke", "--shell-dir", _folder.Combine("missing"));

        Assert.Equal(1, exitCode);
        Assert.Contains(
            Lines(),
            line => line.StartsWith("shell: missing ", StringComparison.Ordinal));
        Assert.Equal("smoke: failed", Lines()[^1]);
    }

    [Fact]
    public void ShowsTheAPIOriginGivenOnTheCommandLine()
    {
        Run(
            "--smoke",
            "--shell-dir",
            TestShell.CreateIn(_folder),
            "--api-origin=https://api.example");

        Assert.Contains("proxy: ready /api/** -> https://api.example", Lines());
    }

    [Fact]
    public async Task ProbesTheAPIThroughTheProxyOnRequest()
    {
        await using var api = await FakeApi.StartAsync(TestContext.Current.CancellationToken);
        var origin = api.Origin.GetLeftPart(UriPartial.Authority);

        var exitCode = Run(
            "--smoke",
            "--probe-api",
            "--shell-dir",
            TestShell.CreateIn(_folder),
            "--api-origin",
            origin);

        Assert.Equal(0, exitCode);
        Assert.Contains("api: reachable HTTP 200 /api/meta", Lines());
    }

    [Theory]
    [InlineData("--api-origin", "http://api.example")]
    [InlineData("--api-origin", "https://api.example/api")]
    [InlineData("--data-dir")]
    public void RefusesACommandLineItCannotRunWith(params string[] args)
    {
        var exitCode = DesktopEntry.Run(["--smoke", .. args], _output, _error);

        Assert.Equal(DesktopEntry.UsageError, exitCode);
        Assert.NotEmpty(_error.ToString());
        Assert.Empty(_output.ToString());
    }

    private int Run(params string[] args) =>
        DesktopEntry.Run([.. args, "--data-dir", _folder.Combine("data")], _output, _error);

    private string[] Lines() =>
        _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
