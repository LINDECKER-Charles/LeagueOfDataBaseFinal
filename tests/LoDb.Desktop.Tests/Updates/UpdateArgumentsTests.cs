using LoDb.Desktop.Updates;

namespace LoDb.Desktop.Tests.Updates;

public sealed class UpdateArgumentsTests
{
    private const string Executable = "/Applications/LoDb.app/Contents/MacOS/LoDb.Desktop";

    [Theory]
    [InlineData(nameof(UpdateMode.Background))]
    [InlineData(nameof(UpdateMode.Background), "--devtools")]
    [InlineData(nameof(UpdateMode.Background), "--apply-updates")]
    [InlineData(nameof(UpdateMode.Off), "--smoke")]
    [InlineData(nameof(UpdateMode.Off), "--smoke", "--probe-api")]
    [InlineData(nameof(UpdateMode.ApplyAtExit), "--smoke", "--apply-updates")]
    [InlineData(nameof(UpdateMode.ApplyAtExit), "--apply-updates", "--smoke")]
    public void ReadsTheModeFromTheCommandLine(string expected, params string[] args)
    {
        var settings = UpdateArguments.Parse([Executable, .. args], NoEnvironment);

        Assert.Equal(Enum.Parse<UpdateMode>(expected), settings.Mode);
    }

    [Fact]
    public void ReadsTheLocalFeedFromTheEnvironment()
    {
        var settings = UpdateArguments.Parse(
            [Executable],
            name => name == UpdateArguments.LocalFeedVariable ? "/tmp/releases" : null);

        Assert.Equal("/tmp/releases", settings.LocalFeed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void KeepsTheGithubFeedWithoutALocalOne(string? value)
    {
        var settings = UpdateArguments.Parse([Executable], _ => value);

        Assert.Null(settings.LocalFeed);
    }

    private static string? NoEnvironment(string name) => null;
}
