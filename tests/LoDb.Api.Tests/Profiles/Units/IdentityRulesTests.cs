using LoDb.Api.Modules.Profiles.Identity;

namespace LoDb.Api.Tests.Profiles.Units;

public sealed class IdentityRulesTests
{
    [Theory]
    [InlineData("EUW")]
    [InlineData("EUW1")]
    [InlineData("12345")]
    public void RiotTaglineOfThreeToFiveLettersOrDigitsPasses(string tagline)
    {
        Assert.True(IdentityRules.Check("Legende_42", tagline).IsEmpty);
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EUWEST")]
    [InlineData("EU 1")]
    [InlineData("EU_1")]
    [InlineData("ÉUW")]
    [InlineData("EUW\n")]
    public void AnyOtherTaglineIsInvalid(string tagline)
    {
        Assert.Equal(["tagline-invalid"], Errors("Legende_42", tagline)["riotTagline"]);
    }

    [Fact]
    public void TaglineIsTrimmedAndBlankMeansNone()
    {
        Assert.Equal("EUW1", IdentityRules.Tagline("  EUW1 "));
        Assert.Null(IdentityRules.Tagline("   "));
        Assert.Null(IdentityRules.Tagline(null));
        Assert.True(IdentityRules.Check("Legende_42", null).IsEmpty);
    }

    [Theory]
    [InlineData("", "required")]
    [InlineData("ab", "username-invalid")]
    [InlineData("_Legende", "username-invalid")]
    public void UsernameFollowsTheRegistrationRules(string username, string code)
    {
        Assert.Equal([code], Errors(username, null)["username"]);
    }

    private static IReadOnlyDictionary<string, string[]> Errors(string username, string? tagline) =>
        IdentityRules.Check(username, tagline).ToProblem().Errors!;
}
