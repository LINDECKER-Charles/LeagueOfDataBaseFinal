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

    // One row per check: absent; a leading dot (which also covers ".." and "../x"), a leading
    // space, a trailing dot, a trailing space; a forbidden character (separators, drive colon
    // and Windows' reserved ones are one set); a control character; a format character; a
    // device name, whatever its case and extension, and with a space before its extension.
    [Theory]
    [InlineData(null)]
    [InlineData(".bashrc")]
    [InlineData(" build.json")]
    [InlineData("build.")]
    [InlineData("build.json ")]
    [InlineData("folder/build.json")]
    [InlineData("build\u0000.json")]
    [InlineData("invoice\u202Etxt.exe")]
    [InlineData("nul.txt")]
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
