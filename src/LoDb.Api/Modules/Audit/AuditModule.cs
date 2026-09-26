namespace LoDb.Api.Modules.Audit;

/// <summary>
/// Audit module: queries, retention and purge of the audit log.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class AuditModule
{
    public static IServiceCollection AddAudit(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapAudit(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
