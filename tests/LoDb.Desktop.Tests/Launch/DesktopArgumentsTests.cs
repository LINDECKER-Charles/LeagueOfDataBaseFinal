using LoDb.Desktop.Launch;

namespace LoDb.Desktop.Tests.Launch;

public sealed class DesktopArgumentsTests
{
    private static readonly DesktopBuild StableBuild = new()
    {
        Version = "1.2.3",
        Channel = DesktopChannels.Stable,
        GoogleClientId = "baked-client",
    };

    private static readonly Dictionary<string, string?> NoVariables = [];

    [Fact]
    public void DefaultsToTheWindowOnTheAPIOfTheChannel()
    {
        var launch = Parse([]);

        Assert.Equal(LaunchMode.Window, launch.Mode);
        Assert.Equal(new Uri("https://league-of-data-base.com"), launch.Options.ApiOrigin);
        Assert.Equal("1.2.3", launch.Options.Version);
        Assert.Equal("baked-client", launch.Options.GoogleClientId);
        Assert.EndsWith(
            Path.Combine("LeagueOfDataBase", "stable"),
            launch.Options.DataDirectory,
            StringComparison.Ordinal);
        Assert.Equal(
            Path.Combine(AppContext.BaseDirectory, "shell"),
            launch.Options.ShellDirectory);
        Assert.False(launch.Options.IsDevToolsEnabled);
        Assert.False(launch.ShouldProbeApi);
    }

    [Fact]
    public void PointsABetaBuildAtTheStagingAPI()
    {
        var launch = Parse([], StableBuild with { Channel = DesktopChannels.Beta });

        Assert.Equal(new Uri("https://test.league-of-data-base.com"), launch.Options.ApiOrigin);
        Assert.EndsWith(
            Path.Combine("LeagueOfDataBase", "beta"),
            launch.Options.DataDirectory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsFlagsInBothFormsAndIgnoresUnknownOnes()
    {
        var launch = Parse(
        [
            "--smoke",
            "--probe-api",
            "--devtools",
            "--data-dir=/data",
            "--shell-dir",
            "/shell",
            "--veloapp-foo",
        ]);

        Assert.Equal(LaunchMode.Smoke, launch.Mode);
        Assert.True(launch.ShouldProbeApi);
        Assert.True(launch.Options.IsDevToolsEnabled);
        Assert.Equal("/data", launch.Options.DataDirectory);
        Assert.Equal("/shell", launch.Options.ShellDirectory);
    }

    [Fact]
    public void TakesSettingsFromTheEnvironmentBelowTheFlags()
    {
        var variables = new Dictionary<string, string?>
        {
            [DesktopArguments.ApiOriginVariable] = "https://env.example",
            [DesktopArguments.DataDirectoryVariable] = "/env-data",
            [DesktopArguments.GoogleClientVariable] = "env-client",
        };

        var fromEnvironment = DesktopArguments.Parse(
            [],
            variables.GetValueOrDefault,
            StableBuild);
        var fromFlag = DesktopArguments.Parse(
            ["--api-origin", "https://flag.example"],
            variables.GetValueOrDefault,
            StableBuild);

        Assert.Equal(new Uri("https://env.example"), fromEnvironment.Options.ApiOrigin);
        Assert.Equal("/env-data", fromEnvironment.Options.DataDirectory);
        Assert.Equal("env-client", fromEnvironment.Options.GoogleClientId);
        Assert.Equal(new Uri("https://flag.example"), fromFlag.Options.ApiOrigin);
    }

    [Theory]
    [InlineData("https://api.example:8443", "https://api.example:8443/")]
    [InlineData("https://api.example/", "https://api.example/")]
    [InlineData("http://127.0.0.1:5080", "http://127.0.0.1:5080/")]
    [InlineData("http://localhost:5080", "http://localhost:5080/")]
    public void AcceptsHttpsOriginsAndCleartextOnLoopback(string value, string origin) =>
        Assert.Equal(new Uri(origin), Parse(["--api-origin", value]).Options.ApiOrigin);

    [Theory]
    [InlineData("http://api.example")]
    [InlineData("https://api.example/api")]
    [InlineData("https://api.example/?x=1")]
    [InlineData("https://user@api.example")]
    [InlineData("ftp://api.example")]
    [InlineData("api.example")]
    public void RefusesOtherOrigins(string value) =>
        Assert.Throws<ArgumentException>(() => Parse(["--api-origin", value]));

    [Fact]
    public void RefusesAFlagWithoutItsValue() =>
        Assert.Throws<ArgumentException>(() => Parse(["--shell-dir"]));

    [Fact]
    public void RefusesAnUnknownChannel() =>
        Assert.Throws<ArgumentException>(
            () => Parse([], StableBuild with { Channel = "nightly" }));

    private static DesktopLaunch Parse(string[] args) => Parse(args, StableBuild);

    private static DesktopLaunch Parse(string[] args, DesktopBuild build) =>
        DesktopArguments.Parse(args, NoVariables.GetValueOrDefault, build);
}
