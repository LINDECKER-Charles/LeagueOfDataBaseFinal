using LoDb.Api.Modules.Accounts.Registration;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The rules of the legacy registration form: an e-mail with a top-level domain, stored
/// lowercase; a username of 3 to 24 characters starting with a letter or a digit; the CNIL
/// password policy; the terms accepted.
/// </summary>
public sealed class RegistrationRulesTests
{
    private const int MaxEmailLength = 180;
    private const string Domain = "@example.test";

    [Fact]
    public void ValidRequestBreaksNoRule() =>
        Assert.True(RegistrationRules.Check(Request()).IsEmpty);

    [Fact]
    public void EmailIsStoredTrimmedAndLowercase() =>
        Assert.Equal("nouveau@example.test", RegistrationRules.Email(" Nouveau@Example.TEST "));

    [Theory]
    [InlineData("", "required")]
    [InlineData("   ", "required")]
    [InlineData("nouveau", "email-invalid")]
    [InlineData("nouveau@localhost", "email-invalid")]
    [InlineData("nou veau@example.test", "email-invalid")]
    [InlineData("nouveau@-example.test", "email-invalid")]
    public void EmailNeedsAnAddressWithATopLevelDomain(string email, string code) =>
        Assert.Equal([code], Errors(Request(email: email))["email"]);

    [Fact]
    public void EmailFitsTheColumn()
    {
        var longest = new string('a', MaxEmailLength - Domain.Length) + Domain;

        Assert.True(RegistrationRules.Check(Request(email: longest)).IsEmpty);
        Assert.Equal(["email-too-long"], Errors(Request(email: "a" + longest))["email"]);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("Legende_42")]
    [InlineData("jean.pierre-2")]
    [InlineData("0123456789abcdefghijklmn")]
    public void UsernameOfTheLegacyPatternIsAccepted(string username) =>
        Assert.True(RegistrationRules.IsUsername(username));

    [Theory]
    [InlineData("ab")]
    [InlineData("_abc")]
    [InlineData(".abc")]
    [InlineData("jean pierre")]
    [InlineData("élodie")]
    [InlineData("0123456789abcdefghijklmno")]
    public void UsernameOutsideTheLegacyPatternIsRefused(string username) =>
        Assert.Equal(["username-invalid"], Errors(Request(username: username))["username"]);

    [Fact]
    public void UsernameIsTrimmedBeforeItsRule()
    {
        Assert.Equal("Legende_42", RegistrationRules.Username("  Legende_42 "));
        Assert.True(RegistrationRules.Check(Request(username: "  Legende_42 ")).IsEmpty);
        Assert.Equal(["required"], Errors(Request(username: " "))["username"]);
    }

    [Fact]
    public void PasswordFollowsThePasswordPolicy()
    {
        Assert.Equal(["required"], Errors(Request(password: string.Empty))["password"]);
        Assert.Contains(
            "auth.password.rule_length",
            Errors(Request(password: "Sh0rt!"))["password"]);
    }

    [Fact]
    public void TermsMustBeAccepted() =>
        Assert.Equal(
            ["terms-required"],
            Errors(Request() with { AcceptTerms = false })["acceptTerms"]);

    private static RegisterRequest Request(
        string email = "nouveau@example.test",
        string username = "Nouveau_7",
        string password = "Str0ng-passphrase!") => new()
        {
            Email = email,
            Username = username,
            Password = password,
            AcceptTerms = true,
        };

    private static IReadOnlyDictionary<string, string[]> Errors(RegisterRequest request) =>
        RegistrationRules.Check(request).ToProblem().Errors!;
}
