using LoDb.Domain.Builds.Metadata;

namespace LoDb.Domain.Tests.Builds.Metadata;

/// <summary>The token of a <c>/b/{token}</c> link: 12 bytes in lowercase hexadecimal.</summary>
public sealed class ShareTokensTests
{
    [Fact]
    public void TwelveBytesMakeATokenOfTheLegacyPattern()
    {
        byte[] bytes = [0x00, 0x01, 0xab, 0xcd, 0xef, 0x10, 0x99, 0xff, 0x42, 0x07, 0x7f, 0x80];

        var token = ShareTokens.Format(bytes);

        Assert.Equal("0001abcdef1099ff42077f80", token);
        Assert.Matches(ShareTokens.Pattern, token);
        Assert.True(ShareTokens.IsWellFormed(token));
    }

    [Fact]
    public void AnotherByteCountIsRefused() =>
        Assert.Throws<ArgumentException>(static () => ShareTokens.Format(new byte[16]));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0001abcdef1099ff42077f8")]
    [InlineData("0001abcdef1099ff42077f800")]
    [InlineData("0001ABCDEF1099FF42077F80")]
    [InlineData("0001abcdef1099ff42077fzz")]
    public void AnyOtherShapeIsNoToken(string? token) =>
        Assert.False(ShareTokens.IsWellFormed(token));
}
