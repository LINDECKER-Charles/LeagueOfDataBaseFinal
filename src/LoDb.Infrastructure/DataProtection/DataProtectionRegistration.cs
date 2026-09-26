using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.DataProtection;

/// <summary>
/// Registrations of the Data Protection zone: the key ring shared by every instance.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class DataProtectionRegistration
{
    public static IServiceCollection AddLoDbDataProtection(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
