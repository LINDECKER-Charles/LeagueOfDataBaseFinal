using LoDb.Domain.States;

namespace LoDb.Domain.Tests.States;

/// <summary>
/// One key per entity whatever the case Data Dragon gave its id in a version.
/// </summary>
public sealed class EntityKeyTests
{
    [Theory]
    [InlineData("FiddleSticks", "fiddlesticks")]
    [InlineData("Fiddlesticks", "fiddlesticks")]
    [InlineData("SummonerFlash_Jade", "summonerflash_jade")]
    [InlineData("3001", "3001")]
    public void IdIsFolded(string id, string key)
    {
        Assert.Equal(key, EntityKey.Of(id));
    }

    [Fact]
    public void RunePathIdIsInvariant()
    {
        Assert.Equal("8000", EntityKey.Of(8000));
    }
}
