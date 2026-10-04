using LoDb.Domain.Derived.Champions;

namespace LoDb.Domain.Tests.Derived.Champions;

/// <summary>
/// Old versions omit a skin's number: its position in the list stands in for it.
/// </summary>
public sealed class SkinNumberTests
{
    [Theory]
    [InlineData(27, 3, 27)]
    [InlineData(0, 5, 0)]
    public void TheShippedNumberWins(int? num, int index, int expected)
    {
        Assert.Equal(expected, SkinNumber.Of(num, index));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Up3AMissingNumberFallsBackToTheIndex(int index)
    {
        Assert.Equal(index, SkinNumber.Of(null, index));
    }
}
