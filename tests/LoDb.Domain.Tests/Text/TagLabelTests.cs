using LoDb.Domain.Text;

namespace LoDb.Domain.Tests.Text;

/// <summary>
/// A category tag reads as words; acronyms stay whole.
/// </summary>
public sealed class TagLabelTests
{
    [Theory]
    [InlineData("CriticalStrike", "Critical Strike")]
    [InlineData("NonbootsMovement", "Nonboots Movement")]
    [InlineData("ARAM", "ARAM")]
    [InlineData("Boots", "Boots")]
    [InlineData(null, "")]
    public void CamelCaseTagsSplitIntoWords(string? tag, string expected)
    {
        Assert.Equal(expected, DdragonText.TagLabel(tag));
    }
}
