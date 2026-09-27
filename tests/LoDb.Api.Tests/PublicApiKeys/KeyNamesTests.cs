using LoDb.Api.Modules.PublicApi.Keys;

namespace LoDb.Api.Tests.PublicApiKeys;

/// <summary>The name of a key, as the legacy issuer stores it.</summary>
public sealed class KeyNamesTests
{
    [Theory]
    [InlineData(null, "default")]
    [InlineData("   ", "default")]
    [InlineData("  my-app ", "my-app")]
    public void BlankNamesAreDefaultAndOthersTrimmed(string? name, string stored) =>
        Assert.Equal(stored, KeyNames.Normalize(name));

    [Fact]
    public void LongNamesAreCutToTheColumn() =>
        Assert.Equal(new string('a', 64), KeyNames.Normalize(new string('a', 80)));

    [Fact]
    public void ACutNeverSplitsASurrogatePair()
    {
        var name = new string('a', 63) + "😀";

        Assert.Equal(new string('a', 63), KeyNames.Normalize(name));
    }
}
