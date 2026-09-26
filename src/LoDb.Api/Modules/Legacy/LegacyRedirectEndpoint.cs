using LoDb.Api.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Legacy;

/// <summary>
/// <c>GET /api/legacy/{path}</c>: nginx forwards every old URL here, path and query as the
/// browser sent them (<c>legacy-redirects.conf</c>).
/// </summary>
/// <remarks>
/// Left out of the OpenAPI document: no client calls it, and the generated front client must
/// not grow a service for it.
/// </remarks>
internal static class LegacyRedirectEndpoint
{
    /// <summary>Prefix nginx writes before the old path.</summary>
    public const string Prefix = ApiPaths.App + "/legacy";

    private const string LanguageQuery = "lang";
    private const string VersionQuery = "version";

    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapMethods(
                Prefix + "/{**path}",
                [HttpMethods.Get, HttpMethods.Head],
                static (
                    string? path,
                    HttpRequest request,
                    [FromServices] LegacyResolver resolver) =>
                    resolver.ResolveAsync(
                        new LegacyRequest(
                            path,
                            request.Query[LanguageQuery].ToString(),
                            request.Query[VersionQuery].ToString()),
                        request.HttpContext.RequestAborted))
            .ExcludeFromDescription();
}
