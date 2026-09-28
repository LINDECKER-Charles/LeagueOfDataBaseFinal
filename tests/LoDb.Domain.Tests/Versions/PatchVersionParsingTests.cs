using System.Text.RegularExpressions;
using LoDb.Domain.Versions;

namespace LoDb.Domain.Tests.Versions;

/// <summary>
/// A version is dotted ASCII numbers, two segments at least, kept verbatim: it names the
/// datasets, the URLs and the storage keys.
/// </summary>
public sealed class PatchVersionParsingTests
{
    [Theory]
    [InlineData("16.14.1")]
    [InlineData("0.151.2")]
    [InlineData("7.22.1")]
    [InlineData("15.1")]
    [InlineData("3.6.14")]
    public void WellFormedVersionsParseVerbatim(string text)
    {
        Assert.True(PatchVersion.TryParse(text, out var version));
        Assert.Equal(text, version.Value);
        Assert.Equal(text, version.ToString());
    }

    [Theory]
    [InlineData("lolpatch_3.7")]
    [InlineData("lolpatch_4.21")]
    [InlineData("16")]
    [InlineData("16.")]
    [InlineData(".16.1")]
    [InlineData("16..1")]
    [InlineData("16.1.1\n")]
    [InlineData(" 16.1.1")]
    [InlineData("16.1/../1")]
    [InlineData("١٦.١")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsRejected(string? text)
    {
        Assert.False(PatchVersion.TryParse(text, out var version));
        Assert.Null(version);
    }

    [Fact]
    public void ParseRejectsAMalformedVersionLoudly()
    {
        Assert.Throws<FormatException>(() => PatchVersion.Parse("lolpatch_3.7"));
    }

    [Theory]
    [InlineData("16.14.1")]
    [InlineData("0.151.2")]
    [InlineData("lolpatch_3.7")]
    [InlineData("16")]
    [InlineData("16.1.1\n")]
    public void ThePublishedPatternAgreesWithTheParser(string text)
    {
        var contract = new Regex(
            $"^(?:{PatchVersion.Pattern})$",
            RegexOptions.ECMAScript,
            TimeSpan.FromSeconds(1));

        var isContractMatch = contract.IsMatch(text) && !text.EndsWith('\n');

        Assert.Equal(isContractMatch, PatchVersion.TryParse(text, out _));
    }
}
