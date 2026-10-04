using LoDb.Desktop.Bridge.Validation;

namespace LoDb.Desktop.Tests.Bridge;

public sealed class SaveFileNameTests
{
    [Theory]
    [InlineData("build.json")]
    [InlineData("Ahri - mid (14.2).txt")]
    [InlineData("export")]
    [InlineData("été_2026.csv")]
    [InlineData("console.log.txt")]
    public void AcceptsPlainNames(string name) => Assert.True(SaveFileName.IsValid(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(".bashrc")]
    [InlineData("../build.json")]
    [InlineData("folder/build.json")]
    [InlineData("folder\\build.json")]
    [InlineData("/etc/passwd")]
    [InlineData("C:build.json")]
    [InlineData("build?.json")]
    [InlineData("build*.json")]
    [InlineData("build<1>.json")]
    [InlineData("build|json")]
    [InlineData("\"build\".json")]
    [InlineData("build.json ")]
    [InlineData(" build.json")]
    [InlineData("build.")]
    [InlineData("build\u0000.json")]
    [InlineData("build\n.json")]
    [InlineData("invoice\u202Etxt.exe")]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("Com1.json")]
    [InlineData("LPT9 .log")]
    public void RefusesPathsAndNamesASystemWouldMisread(string? name) =>
        Assert.False(SaveFileName.IsValid(name));

    [Fact]
    public void RefusesNamesLongerThanTheLimit()
    {
        var name = new string('a', SaveFileName.MaxLength) + ".json";

        Assert.False(SaveFileName.IsValid(name));
    }
}
