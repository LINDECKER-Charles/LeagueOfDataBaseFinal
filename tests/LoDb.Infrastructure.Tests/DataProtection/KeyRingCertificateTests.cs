using LoDb.Infrastructure.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests.DataProtection;

/// <summary>
/// A certificate that is set but unusable stops the registration, hence the host, with a
/// message that names the setting and never the password.
/// </summary>
public sealed class KeyRingCertificateTests
{
    private const string Unreadable =
        "The certificate of LoDb:DataProtection:CertificatePath cannot be read with its password.";

    [Fact]
    public void MissingFileStopsTheRegistration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["LoDb:DataProtection:CertificatePath"] =
                Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pfx"),
        };

        var error = Assert.Throws<InvalidOperationException>(() => Register(settings));

        Assert.Equal(Unreadable, error.Message);
    }

    [Fact]
    public void WrongPasswordStopsTheRegistration()
    {
        using var certificate = TemporaryCertificate.Create(withPrivateKey: true);

        var error = Assert.Throws<InvalidOperationException>(
            () => Register(certificate.Settings("not-the-password")));

        Assert.Equal(Unreadable, error.Message);
    }

    [Fact]
    public void CertificateWithoutItsPrivateKeyStopsTheRegistration()
    {
        using var certificate = TemporaryCertificate.Create(withPrivateKey: false);

        var error = Assert.Throws<InvalidOperationException>(
            () => Register(certificate.Settings()));

        Assert.Equal(
            "The certificate of LoDb:DataProtection:CertificatePath has no private key.",
            error.Message);
    }

    private static void Register(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        new ServiceCollection().AddLoDbDataProtection(configuration);
    }
}
