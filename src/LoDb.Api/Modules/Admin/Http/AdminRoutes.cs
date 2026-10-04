using LoDb.Api.Hosting;

namespace LoDb.Api.Modules.Admin.Http;

/// <summary>Paths and OpenAPI tags of the admin API.</summary>
/// <remarks>
/// The audit journal (<c>/api/admin/audit</c>) and the analytics reports
/// (<c>/api/admin/analytics</c>) live in their own modules under the same prefix.
/// </remarks>
internal static class AdminRoutes
{
    /// <summary>Prefix of every admin endpoint.</summary>
    public const string Prefix = ApiPaths.App + "/admin";

    // The generated client gets one service per tag.
    public const string MfaTag = "AdminMfa";
    public const string UsersTag = "AdminUsers";
    public const string BuildsTag = "AdminBuilds";
    public const string ContactsTag = "AdminContacts";
    public const string ApiClientsTag = "AdminApiClients";
    public const string DonationsTag = "AdminDonations";
    public const string MonitoringTag = "AdminMonitoring";
    public const string StorageTag = "AdminStorage";
}
