using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LoDb.Infrastructure.DataProtection;

/// <summary>Reads the certificate that encrypts the Data Protection keys.</summary>
internal static class KeyRingCertificate
{
    /// <summary>
    /// The certificate of <paramref name="settings"/>, with the private key that reads the
    /// keys back.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The file is missing or unreadable, the password is wrong or the private key is
    /// missing: the host must not start with a key ring it could not read.
    /// </exception>
    public static X509Certificate2 Load(KeyRingOptions settings)
    {
        ArgumentException.ThrowIfNullOrEmpty(settings.CertificatePath);
        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12FromFile(
                settings.CertificatePath,
                settings.CertificatePassword);
        }
        catch (Exception exception) when (exception
            is IOException or UnauthorizedAccessException or CryptographicException)
        {
            throw Unreadable("cannot be read with its password", exception);
        }

        if (certificate.HasPrivateKey)
        {
            return certificate;
        }

        certificate.Dispose();
        throw Unreadable("has no private key", innerException: null);
    }

    // The message names the setting, never the password.
    private static InvalidOperationException Unreadable(string why, Exception? innerException) =>
        new($"The certificate of {KeyRingOptions.SectionName}:CertificatePath {why}.",
            innerException);
}
