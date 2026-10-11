using LoDb.Api.Modules.Accounts.SignIn;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The bounds of the fields of a sign-in (<see cref="SignInFieldBounds"/>): an identifier no
/// longer than an e-mail once trimmed, an authenticator code of digits that the apps group
/// with spaces or a dash, a recovery code of letters and digits.
/// </summary>
public sealed class SignInFieldShapeTests
{
    private const int LongestIdentifier = 254;
    private const int LongestAuthenticatorCode = 16;
    private const int LongestRecoveryCode = 32;

    public static TheoryData<string, bool> Identifiers => new()
    {
        { new string('a', LongestIdentifier), true },
        { $"  {new string('a', LongestIdentifier)} ", true },
        { new string('a', LongestIdentifier + 1), false },
    };

    public static TheoryData<string, bool> AuthenticatorCodes => new()
    {
        { "123456", true },
        { "123 456", true },
        { "123-456", true },
        { new string('1', LongestAuthenticatorCode), true },
        { new string('1', LongestAuthenticatorCode + 1), false },
        { "12345a", false },
        { "<script>", false },
    };

    public static TheoryData<string, bool> RecoveryCodes => new()
    {
        { "ab3cd-ef4gh", true },
        { "AB3CD EF4GH", true },
        { new string('A', LongestRecoveryCode), true },
        { new string('A', LongestRecoveryCode + 1), false },
        { "AB3CD;EF4GH", false },
    };

    [Theory]
    [MemberData(nameof(Identifiers))]
    public void IdentifierIsNoLongerThanAnEmail(string identifier, bool isIdentifier) =>
        Assert.Equal(isIdentifier, SignInFieldBounds.IsIdentifier(identifier));

    [Theory]
    [MemberData(nameof(AuthenticatorCodes))]
    public void AuthenticatorCodeIsGroupedDigits(string code, bool isCode) =>
        Assert.Equal(isCode, SignInFieldBounds.IsAuthenticatorCode(code));

    [Theory]
    [MemberData(nameof(RecoveryCodes))]
    public void RecoveryCodeIsLettersAndDigits(string code, bool isCode) =>
        Assert.Equal(isCode, SignInFieldBounds.IsRecoveryCode(code));
}
