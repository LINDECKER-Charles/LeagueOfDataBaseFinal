using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Tests.Builds.Structures;

/// <summary>
/// Int-shaped strings read as the legacy <c>IntegerValue</c> read them: canonical decimal
/// only, whatever PHP's own cast would have accepted.
/// </summary>
public sealed class IntegerTextTests
{
    [Theory]
    [InlineData("8000", 8000)]
    [InlineData("0", 0)]
    [InlineData("-3", -3)]
    [InlineData("2147483647", int.MaxValue)]
    public void CanonicalDigitsRead(string text, int expected) =>
        Assert.Equal(expected, IntegerText.Read(text));

    // Absent, then one row per way a text fails: not a number at all, a number written
    // otherwise than canonically, and one past int.MaxValue.
    [Theory]
    [InlineData(null)]
    [InlineData("8000.0")]
    [InlineData("08000")]
    [InlineData("2147483648")]
    public void AnythingElseReadsAsNothing(string? text) => Assert.Null(IntegerText.Read(text));
}
