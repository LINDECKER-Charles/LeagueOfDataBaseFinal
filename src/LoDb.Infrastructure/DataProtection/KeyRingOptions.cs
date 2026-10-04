namespace LoDb.Infrastructure.DataProtection;

/// <summary>
/// Settings of the Data Protection key ring, bound from <c>LoDb:DataProtection</c>.
/// </summary>
internal sealed class KeyRingOptions
{
    public const string SectionName = "LoDb:DataProtection";

    /// <summary>
    /// PKCS#12 file (.pfx) holding the certificate that encrypts the keys and its private
    /// key, mounted as a secret. Only development may leave it unset.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>Password of the PKCS#12 file, a secret too; null when it has none.</summary>
    public string? CertificatePassword { get; set; }
}
