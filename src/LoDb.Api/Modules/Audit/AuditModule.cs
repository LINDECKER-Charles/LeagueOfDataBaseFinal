using LoDb.Api.Hosting;
using LoDb.Api.Modules.Audit.Http;
using LoDb.Api.Modules.Audit.Import;
using LoDb.Api.Modules.Audit.Purge;
using LoDb.Api.Modules.Audit.Reading;
using LoDb.Api.Modules.Audit.Retention;
using LoDb.Api.Modules.Audit.Vocabulary;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Audit;

/// <summary>
/// Audit module: queries, retention and purge of the audit log.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw. The journal is
/// written by <c>IAuditLog</c> (L4.1); the daily retention runs in
/// <c>Workers/Audit</c>, the import of the legacy journal in <c>Cli/Audit</c>.
/// </remarks>
internal static class AuditModule
{
    public static IServiceCollection AddAudit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<AuditRetention>();
        services.TryAddScoped<AuditReader>();
        services.TryAddScoped<LegacyAuditImport>();
        services.TryAddScoped<PurgeEndpoint>();
        return services;
    }

    public static IEndpointRouteBuilder MapAudit(this IEndpointRouteBuilder endpoints)
    {
        // Reads the IP addresses of the journal: never cached, whatever sits in between.
        var audit = endpoints.MapGroup(AuditRoutes.Prefix)
            .WithTags(AuditRoutes.Tag)
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .AddEndpointFilter<NoStoreFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        JournalEndpoint.Map(audit);
        UserActivityEndpoint.Map(audit);
        VolumeEndpoint.Map(audit);
        VocabularyEndpoint.Map(audit);
        PurgeEndpoint.Map(audit);
        return endpoints;
    }
}
