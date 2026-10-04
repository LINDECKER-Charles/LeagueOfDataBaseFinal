using LoDb.Domain.Derived.Runes;

namespace LoDb.Domain.Tests.Derived.Runes;

/// <summary>
/// A rune's row is positional: the first is the keystone, the others are numbered.
/// </summary>
public sealed class RuneSlotTokenTests
{
    [Theory]
    [InlineData(0, "keystone")]
    [InlineData(1, "row1")]
    [InlineData(3, "row3")]
    public void TheSlotIndexNamesTheRow(int slotIndex, string expected)
    {
        Assert.Equal(expected, RuneSlotToken.Of(slotIndex));
    }
}
