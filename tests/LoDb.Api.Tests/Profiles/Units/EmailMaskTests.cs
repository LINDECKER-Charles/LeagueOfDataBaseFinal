using LoDb.Api.Modules.Profiles.Owner;

namespace LoDb.Api.Tests.Profiles.Units;

public sealed class EmailMaskTests
{
    [Theory]
    [InlineData("legende@example.test", "l***@example.test")]
    [InlineData("a@b.c", "a***@b.c")]
    [InlineData("😀joueur@example.test", "😀***@example.test")]
    [InlineData("@example.test", "***")]
    [InlineData("sans-arobase", "***")]
    [InlineData(null, "***")]
    public void OnlyTheFirstCharacterAndTheDomainShow(string? email, string masked)
    {
        Assert.Equal(masked, EmailMask.Apply(email));
    }
}
