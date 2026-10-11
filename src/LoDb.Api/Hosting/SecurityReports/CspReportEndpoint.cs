using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Hosting.SecurityReports;

/// <summary>
/// <c>POST /api/csp-reports</c>: the violations of the pages' Content-Security-Policy, as
/// browsers report them (<c>report-to</c> and <c>report-uri</c>).
/// </summary>
/// <remarks>
/// <para>
/// No account involved, yet never <c>AllowAnonymous</c>: the default policy checks the
/// <c>Origin</c> of the report, which a browser sets to the page's own. A report that carries
/// the session cookie without the XSRF token is refused as any such request: a signed-in
/// visitor's report is lost when the browser attaches cookies to it (<c>report-uri</c>); the
/// Reporting API sends none.
/// </para>
/// <para>
/// Called by browsers alone, out of the generated client: kept out of the contract.
/// </para>
/// </remarks>
internal static class CspReportEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                SecurityReportsRoutes.CspPath,
                static (HttpContext context, [FromServices] CspReportIntake intake) =>
                    intake.TakeAsync(context))
            .RequireRateLimiting(SecurityReportsRoutes.PerClientPolicy)
            .ExcludeFromDescription();
}
