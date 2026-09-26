using LoDb.Api.Modules.PublicApi.Trends.Reading;

namespace LoDb.Api.Tests.PublicApi.Units;

/// <summary>
/// The <c>entities</c> map of a daily aggregate, read as go-api decoded its daily files: a
/// day it could not decode is skipped whole.
/// </summary>
public sealed class TrendDayTests
{
    private const string Champion = "champion";

    [Fact]
    public void TheViewsOfTheTypeAddUp()
    {
        var views = new Dictionary<string, long> { ["Ahri"] = 1 };

        var read = TrendDay.TryAdd(
            """{"champion:Ahri": 3, "champion:Zed": 2, "item:3006": 5}""",
            Champion,
            views);

        Assert.True(read);
        Assert.Equal(new Dictionary<string, long> { ["Ahri"] = 4, ["Zed"] = 2 }, views);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"champion:": 3, "championX:Ahri": 4, "Ahri": 5}""")]
    public void ADayWithoutViewsOfTheTypeCountsForNothing(string? entities)
    {
        var views = new Dictionary<string, long>();

        Assert.True(TrendDay.TryAdd(entities, Champion, views));
        Assert.Empty(views);
    }

    [Fact]
    public void ANullCountReadsAsZero()
    {
        var views = new Dictionary<string, long>();

        Assert.True(TrendDay.TryAdd("""{"champion:Ahri": null}""", Champion, views));
        Assert.Equal(0, views["Ahri"]);
    }

    [Theory]
    [InlineData("""{"champion:Ahri": 3, "champion:Zed": "2"}""")]
    [InlineData("""{"champion:Ahri": 3, "item:3006": 1.5}""")]
    [InlineData("""{"champion:Ahri": 3, "champion:Zed": 99999999999999999999}""")]
    [InlineData("""{"champion:Ahri": {"views": 3}}""")]
    [InlineData("[1, 2]")]
    [InlineData("\"entities\"")]
    [InlineData("{\"champion:Ahri\": 3")]
    public void ADayGoCouldNotDecodeIsSkippedWhole(string entities)
    {
        var views = new Dictionary<string, long> { ["Ahri"] = 1 };

        Assert.False(TrendDay.TryAdd(entities, Champion, views));
        Assert.Equal(new Dictionary<string, long> { ["Ahri"] = 1 }, views);
    }
}
