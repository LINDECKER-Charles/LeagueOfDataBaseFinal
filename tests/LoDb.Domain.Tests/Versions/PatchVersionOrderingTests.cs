using LoDb.Domain.Versions;

namespace LoDb.Domain.Tests.Versions;

/// <summary>
/// Versions compare segment by segment as numbers, never as text: "10.1" follows "9.24".
/// </summary>
public sealed class PatchVersionOrderingTests
{
    [Theory]
    [InlineData("9.24.2", "10.1.1")]
    [InlineData("15.9.1", "15.10.1")]
    [InlineData("15.1", "15.1.1")]
    [InlineData("0.151.2", "3.6.14")]
    [InlineData("4.20.1", "4.21.1")]
    [InlineData("99999999999999999999.1", "100000000000000000000.1")]
    public void OlderComesFirst(string older, string newer)
    {
        var olderVersion = PatchVersion.Parse(older);
        var newerVersion = PatchVersion.Parse(newer);

        Assert.True(olderVersion.CompareTo(newerVersion) < 0);
        Assert.True(newerVersion.CompareTo(olderVersion) > 0);
        Assert.True(olderVersion < newerVersion);
        Assert.True(newerVersion > olderVersion);
        Assert.True(olderVersion <= newerVersion);
        Assert.True(newerVersion >= olderVersion);
    }

    [Fact]
    public void EqualVersionsAreEqualAndCompareEqual()
    {
        var left = PatchVersion.Parse("16.14.1");
        var right = PatchVersion.Parse("16.14.1");

        Assert.Equal(left, right);
        Assert.Equal(0, left.CompareTo(right));
        Assert.True(left <= right);
        Assert.True(left >= right);
    }

    [Fact]
    public void NumericallyEqualSpellingsStayDistinctButOrdered()
    {
        var padded = PatchVersion.Parse("7.02");
        var plain = PatchVersion.Parse("7.2");

        Assert.NotEqual(padded, plain);
        Assert.NotEqual(0, padded.CompareTo(plain));
        Assert.Equal(-Math.Sign(padded.CompareTo(plain)), Math.Sign(plain.CompareTo(padded)));
    }

    [Fact]
    public void AVersionFollowsNothing()
    {
        var version = PatchVersion.Parse("16.14.1");

        Assert.True(version.CompareTo(null) > 0);
        Assert.True(version > null);
        Assert.True(null < version);
    }
}
