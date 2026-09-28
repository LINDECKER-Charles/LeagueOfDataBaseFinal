using LoDb.Domain.Text;

namespace LoDb.Domain.Tests.Text;

/// <summary>
/// A title gets its first letter uppercased and nothing else touched.
/// </summary>
public sealed class UpperFirstTests
{
    [Theory]
    [InlineData("force de Demacia", "Force de Demacia")]
    [InlineData("épée darkin", "Épée darkin")]
    [InlineData("the Nine-Tailed Fox", "The Nine-Tailed Fox")]
    [InlineData("九尾妖狐", "九尾妖狐")]
    public void Up13TitlesAreNeverLowercased(string title, string expected)
    {
        Assert.Equal(expected, DdragonText.UpperFirst(title));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NothingStaysEmpty(string? title)
    {
        Assert.Equal(string.Empty, DdragonText.UpperFirst(title));
    }

    [Fact]
    public void ALetterOutsideTheBasicPlaneIsUppercasedWhole()
    {
        Assert.Equal("𐐀bc", DdragonText.UpperFirst("𐐨bc"));
    }
}
