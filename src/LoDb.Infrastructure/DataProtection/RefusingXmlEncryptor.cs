using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace LoDb.Infrastructure.DataProtection;

/// <summary>
/// Stands for the missing certificate outside development: a key is never stored in clear,
/// so none is created.
/// </summary>
internal sealed class RefusingXmlEncryptor : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement) =>
        throw new InvalidOperationException(
            "No certificate encrypts the Data Protection keys: set "
            + KeyRingOptions.SectionName + ":CertificatePath.");
}
