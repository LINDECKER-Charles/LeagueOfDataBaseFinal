using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.DataProtection;

/// <summary>
/// Registrations of the Data Protection zone: the key ring shared by every instance.
/// </summary>
/// <remarks>
/// <para>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host. The
/// keys live in <c>data_protection_keys</c>, encrypted by the certificate of
/// <c>LoDb:DataProtection</c>.
/// </para>
/// <para>
/// One exception to the rule: a certificate that is set is read here, and a bad one stops
/// the host. Data Protection takes the certificates that decrypt the keys at registration
/// only; the OpenAPI generation sets none, so it still does no I/O.
/// </para>
/// </remarks>
public static class DataProtectionRegistration
{
    /// <summary>
    /// Isolates the payloads of the API from other applications, and makes every instance
    /// and every release read the same ones; changing it signs everybody out.
    /// </summary>
    public const string ApplicationName = "LoDb";

    public static IServiceCollection AddLoDbDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(KeyRingOptions.SectionName)
            .Get<KeyRingOptions>() ?? new KeyRingOptions();
        var builder = services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<LoDbDbContext>();
        if (string.IsNullOrEmpty(settings.CertificatePath))
        {
            services.TryAddEnumerable(ServiceDescriptor
                .Singleton<IConfigureOptions<KeyManagementOptions>, MissingCertificateSetup>());
        }
        else
        {
            builder.ProtectKeysWithCertificate(KeyRingCertificate.Load(settings));
        }

        return services;
    }
}
