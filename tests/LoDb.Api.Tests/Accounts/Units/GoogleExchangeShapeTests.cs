using LoDb.Api.Modules.Accounts.Google.Apps;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The shapes an app's Google code and PKCE verifier must have before the code is sent to
/// Google: a code of visible ASCII up to 512 characters, a verifier of 43 to 128 unreserved
/// characters.
/// </summary>
public sealed class GoogleExchangeShapeTests
{
    private const int LongestCode = 512;
    private const int ShortestVerifier = 43;
    private const int LongestVerifier = 128;

    public static TheoryData<string, bool> Codes => new()
    {
        { "4/0AeanS0b-x_~.!", true },
        { new string('a', LongestCode), true },
        { new string('a', LongestCode + 1), false },
        { string.Empty, false },
        { "code with spaces", false },
        { "code-été", false },
    };

    public static TheoryData<string, bool> Verifiers => new()
    {
        { new string('v', ShortestVerifier), true },
        { new string('v', LongestVerifier), true },
        { new string('v', ShortestVerifier - 1), false },
        { new string('v', LongestVerifier + 1), false },
        { "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjX~", true },
        { "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjX+", false },
    };

    [Theory]
    [MemberData(nameof(Codes))]
    public void CodeIsVisibleAsciiWithinItsBound(string code, bool isCode) =>
        Assert.Equal(isCode, GoogleExchangeShape.IsCode(code));

    [Theory]
    [MemberData(nameof(Verifiers))]
    public void VerifierIsUnreservedCharactersWithinItsBounds(string verifier, bool isVerifier) =>
        Assert.Equal(isVerifier, GoogleExchangeShape.IsVerifier(verifier));
}
