using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.DataProtection;

/// <summary>
/// The key ring without a certificate: development stores its keys in clear with a warning,
/// any other environment refuses to create them.
/// </summary>
/// <remarks>
/// Refusing, rather than failing the start: the host still starts, which the OpenAPI
/// generation needs, and only what protects data fails, loudly, while no key ever reaches
/// the database in clear.
/// </remarks>
internal sealed partial class MissingCertificateSetup(
    IHostEnvironment environment,
    ILogger<MissingCertificateSetup> logger) : IConfigureOptions<KeyManagementOptions>
{
    public void Configure(KeyManagementOptions options)
    {
        if (environment.IsDevelopment())
        {
            LogUnencrypted(logger);
            return;
        }

        LogCertificateMissing(logger);
        options.XmlEncryptor = new RefusingXmlEncryptor();
    }

    [LoggerMessage(
        EventName = "dataprotection.keys.unencrypted",
        Level = LogLevel.Warning,
        Message = "No certificate is set: Data Protection keys are stored in clear, "
            + "which only development allows.")]
    private static partial void LogUnencrypted(ILogger logger);

    [LoggerMessage(
        EventName = "dataprotection.certificate.missing",
        Level = LogLevel.Critical,
        Message = "No certificate is set: no Data Protection key can be created, so sign-in "
            + "and tokens fail until LoDb:DataProtection:CertificatePath is set.")]
    private static partial void LogCertificateMissing(ILogger logger);
}
