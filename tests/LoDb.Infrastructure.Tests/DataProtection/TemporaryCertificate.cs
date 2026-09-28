using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LoDb.Infrastructure.Tests.DataProtection;

/// <summary>
/// A self-signed certificate in a PKCS#12 file of its own, as the secret of
/// <c>LoDb:DataProtection</c> is mounted; the file goes with the value.
/// </summary>
internal sealed class TemporaryCertificate : IDisposable
{
    public const string Password = "key-ring-test-password";

    private const int KeySize = 2048;

    private readonly DirectoryInfo _directory;

    private TemporaryCertificate(DirectoryInfo directory, string path)
    {
        _directory = directory;
        FilePath = path;
    }

    public string FilePath { get; }

    /// <summary>The settings that point the key ring at this file.</summary>
    public Dictionary<string, string?> Settings(string password = Password) => new()
    {
        ["LoDb:DataProtection:CertificatePath"] = FilePath,
        ["LoDb:DataProtection:CertificatePassword"] = password,
    };

    /// <summary>A new certificate, with or without the private key in the file.</summary>
    public static TemporaryCertificate Create(bool withPrivateKey)
    {
        using var key = RSA.Create(KeySize);
        var request = new CertificateRequest(
            "CN=LoDb Data Protection test",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
        var bytes = (withPrivateKey ? certificate : publicOnly)
            .Export(X509ContentType.Pkcs12, Password);

        var directory = Directory.CreateTempSubdirectory("lodb-key-ring-");
        var path = Path.Combine(directory.FullName, "key-ring.pfx");
        File.WriteAllBytes(path, bytes);
        return new TemporaryCertificate(directory, path);
    }

    public void Dispose() => _directory.Delete(recursive: true);
}
