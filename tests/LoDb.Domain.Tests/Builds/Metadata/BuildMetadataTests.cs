using LoDb.Domain.Builds.Metadata;

namespace LoDb.Domain.Tests.Builds.Metadata;

/// <summary>The texts around a structure, as the legacy submission tests state them.</summary>
public sealed class BuildMetadataTests
{
    // One character beyond the Basic Multilingual Plane: two UTF-16 code units.
    private const string Shield = "\U0001F6E1";

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public void ANameHas3To80Characters(int length, bool isValid) =>
        Assert.Equal(isValid, BuildMetadata.IsNameValid(new string('n', length)));

    [Fact]
    public void ANameCountsCharactersNotCodeUnits() =>
        Assert.True(BuildMetadata.IsNameValid(string.Concat(Enumerable.Repeat(Shield, 80))));

    [Fact]
    public void ANameIsTrimmedBeforeItIsCounted()
    {
        var name = BuildMetadata.Name("  ab  ");

        Assert.Equal("ab", name);
        Assert.False(BuildMetadata.IsNameValid(name));
        Assert.Equal(string.Empty, BuildMetadata.Name(null));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  Snowball early.  ", "Snowball early.")]
    public void ADescriptionIsTrimmedAndBlankMeansNone(string? submitted, string? stored) =>
        Assert.Equal(stored, BuildMetadata.Description(submitted));

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void ADescriptionHas2000CharactersAtMost(int length, bool isValid) =>
        Assert.Equal(isValid, BuildMetadata.IsDescriptionValid(new string('d', length)));

    [Fact]
    public void NoDescriptionIsValid() => Assert.True(BuildMetadata.IsDescriptionValid(null));
}
