using LoDb.Domain.Derived.Ranges;

namespace LoDb.Domain.Tests.Derived.Ranges;

/// <summary>
/// Placeholder ranges are masked rather than shown as distances.
/// </summary>
public sealed class RangeSentinelsTests
{
    [Theory]
    [InlineData("self")]
    [InlineData("25000")]
    [InlineData("25000/25000/25000")]
    [InlineData("4294967295")]
    [InlineData("30000/35000")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("global")]
    public void Up10SentinelRangesAreMasked(string? rangeBurn)
    {
        Assert.Null(RangeSentinels.MaskAbilityRange(rangeBurn));
    }

    [Theory]
    [InlineData("625")]
    [InlineData("600/650/700/750/800")]
    [InlineData("24999")]
    [InlineData("1200/25000")]
    [InlineData("300 units")]
    [InlineData(" 550")]
    [InlineData("0.5")]
    public void MeasurableRangesAreKeptVerbatim(string rangeBurn)
    {
        Assert.Equal(rangeBurn, RangeSentinels.MaskAbilityRange(rangeBurn));
    }

    [Theory]
    [InlineData(25000, true)]
    [InlineData(4294967295, true)]
    [InlineData(24999, false)]
    [InlineData(425, false)]
    public void Up10ASummonerRangeFrom25000IsGlobal(double range, bool isGlobal)
    {
        Assert.Equal(isGlobal, RangeSentinels.IsGlobal(range));
    }
}
