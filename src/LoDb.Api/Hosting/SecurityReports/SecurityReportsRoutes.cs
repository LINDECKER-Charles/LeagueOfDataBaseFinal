namespace LoDb.Api.Hosting.SecurityReports;

/// <summary>Path and rate limiting policy of the browsers' security reports.</summary>
internal static class SecurityReportsRoutes
{
    /// <summary>
    /// Target of the pages' <c>report-uri</c> and <c>Reporting-Endpoints</c>
    /// (src/LoDb.Web/src/server/security/csp/csp-reporting.ts): same origin, under the API.
    /// </summary>
    public const string CspPath = ApiPaths.App + "/csp-reports";

    /// <summary>Rate limiting policy of the reports, by client address.</summary>
    public const string PerClientPolicy = "csp-reports";
}
