using LoDb.Api.Hosting;

namespace LoDb.Api.Modules.Analytics.Admin;

/// <summary>Paths and OpenAPI tag of the analytics reports, under the admin API.</summary>
internal static class AnalyticsAdminRoutes
{
    /// <summary>Prefix of every report endpoint; the admin policy guards the whole group.</summary>
    public const string Prefix = ApiPaths.App + "/admin/analytics";

    /// <summary>The generated client gets one service per tag.</summary>
    public const string Tag = "AdminAnalytics";
}
