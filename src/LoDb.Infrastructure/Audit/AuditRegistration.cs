using LoDb.Infrastructure.Audit.Journal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Infrastructure.Audit;

/// <summary>
/// Registrations of the audit zone: the audit log written by every module.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class AuditRegistration
{
    public static IServiceCollection AddLoDbAudit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // The journal reads the actor, the address and the route of the current request.
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAuditLog, AuditLog>();
        return services;
    }
}
