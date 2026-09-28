using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;

namespace LoDb.Domain.Tests.Paths;

/// <summary>
/// A requested segment names its entity by id whatever its slug says, so a wrong or missing
/// slug can be redirected to the canonical path.
/// </summary>
public sealed class CanonicalPathIdTests
{
    [Theory]
    [InlineData(ResourceType.Items, "1036-long-sword", "1036")]
    [InlineData(ResourceType.Items, "1036-wrong-slug", "1036")]
    [InlineData(ResourceType.Items, "1036", "1036")]
    [InlineData(ResourceType.Runes, "8000-precision", "8000")]
    [InlineData(ResourceType.Champions, "MonkeyKing", "MonkeyKing")]
    [InlineData(ResourceType.Summoners, "SummonerFlash_Jade", "SummonerFlash_Jade")]
    public void TheIdIsReadFromTheSegment(ResourceType type, string segment, string expected)
    {
        Assert.Equal(expected, CanonicalPath.IdOf(type, segment));
    }

    [Theory]
    [InlineData(ResourceType.Items, "long-sword")]
    [InlineData(ResourceType.Items, "-1036")]
    [InlineData(ResourceType.Items, "1036long-sword")]
    [InlineData(ResourceType.Items, "")]
    [InlineData(ResourceType.Champions, "Monkey-King")]
    [InlineData(ResourceType.Champions, "..")]
    [InlineData(ResourceType.Summoners, "")]
    public void ASegmentThatNamesNoEntityHasNoId(ResourceType type, string segment)
    {
        Assert.Null(CanonicalPath.IdOf(type, segment));
    }

    [Fact]
    public void TheCanonicalPathOfARequestedIdMayDifferOnlyByItsSlug()
    {
        var id = CanonicalPath.IdOf(ResourceType.Items, "1036-old-name");

        Assert.NotNull(id);
        Assert.Equal("items/1036-long-sword", CanonicalPath.Item(id, "Long Sword").Value);
    }
}
