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
    [InlineData("15.1")]
    public void WellFormedVersionsParseVerbatim(string text)
    {
        Assert.True(PatchVersion.TryParse(text, out var version));
        Assert.Equal(text, version.Value);
        Assert.Equal(text, version.ToString());
    }

    // One row per class: other characters, a single segment, an empty segment, a trailing
    // line break, digits that are not ASCII, nothing at all.
    [Theory]
    [InlineData("lolpatch_3.7")]
    [InlineData("16")]
    [InlineData("16..1")]
    [InlineData("16.1.1\n")]
    [InlineData("١٦.١")]
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
    [InlineData("16.14.1", true)]
    [InlineData("lolpatch_3.7", false)]
    [InlineData("16", false)]
    [InlineData("16.1.1\n", false)]
    public void ThePublishedPatternAgreesWithTheParser(string text, bool isVersion)
    {
        var contract = new Regex(
            $"^(?:{PatchVersion.Pattern})$",
            RegexOptions.ECMAScript,
            TimeSpan.FromSeconds(1));

        // JavaScript's $ is the end of the input: a match that stops before a final newline,
        // as .NET's $ allows, is none.
        var match = contract.Match(text);

        Assert.Equal(isVersion, match.Success && match.Length == text.Length);
        Assert.Equal(isVersion, PatchVersion.TryParse(text, out _));
    }
}
