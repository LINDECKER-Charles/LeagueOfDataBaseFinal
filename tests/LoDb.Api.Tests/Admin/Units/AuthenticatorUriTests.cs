using LoDb.Api.Modules.Admin.Mfa;

namespace LoDb.Api.Tests.Admin.Units;

/// <summary>The <c>otpauth://</c> URI the enrollment turns into a QR code.</summary>
public sealed class AuthenticatorUriTests
{
    [Fact]
    public void TheUriNamesTheIssuerTheAccountAndTheKey()
    {
        var uri = AuthenticatorUri.For("admin@example.test", "JBSWY3DPEHPK3PXP");

        Assert.Equal(
            "otpauth://totp/LoDb:admin%40example.test?secret=JBSWY3DPEHPK3PXP&issuer=LoDb&digits=6",
            uri);
    }

    [Fact]
    public void TheAccountIsEscaped()
    {
        var uri = AuthenticatorUri.For("a b:c?d&e", "KEY");

        Assert.StartsWith(
            "otpauth://totp/LoDb:a%20b%3Ac%3Fd%26e?secret=KEY&",
            uri,
            StringComparison.Ordinal);
    }
}
