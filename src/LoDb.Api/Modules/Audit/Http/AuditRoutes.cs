using LoDb.Api.Hosting;

namespace LoDb.Api.Modules.Audit.Http;

/// <summary>Paths and OpenAPI tag of the audit journal, under the admin API.</summary>
internal static class AuditRoutes
{
    /// <summary>Prefix of every audit endpoint; the admin policy guards the whole group.</summary>
    public const string Prefix = ApiPaths.App + "/admin/audit";

    /// <summary>The generated client gets one service per tag.</summary>
    public const string Tag = "AdminAudit";
}
