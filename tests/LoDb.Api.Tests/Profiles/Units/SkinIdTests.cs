using LoDb.Api.Modules.Profiles.Favorites;

namespace LoDb.Api.Tests.Profiles.Units;

public sealed class SkinIdTests
{
    [Theory]
    [InlineData("Ahri_1", "Ahri", 1)]
    [InlineData("MonkeyKing_0", "MonkeyKing", 0)]
    [InlineData("Garen_1234", "Garen", 1234)]
    public void WellFormedIdSplitsIntoChampionAndNumber(string text, string champion, int number)
    {
        Assert.True(SkinId.TryParse(text, out var skin));
        Assert.Equal(new SkinId(champion, number), skin);
        Assert.Equal(text, skin.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ahri")]
    [InlineData("Ahri_")]
    [InlineData("Ahri 1")]
    [InlineData("Ahri_12345")]
    [InlineData("Kai'Sa_1")]
    [InlineData("Ahri_1\n")]
    public void AnythingElseIsMalformed(string text)
    {
        Assert.False(SkinId.IsWellFormed(text));
        Assert.False(SkinId.TryParse(text, out _));
    }

    [Fact]
    public void BaseSkinOfAChampionIsItsNumberZero()
    {
        Assert.Equal(new SkinId("Teemo", 0), SkinId.BaseOf("Teemo"));
        Assert.Null(SkinId.BaseOf(null));
        Assert.Null(SkinId.BaseOf("Kai'Sa"));
    }
}
