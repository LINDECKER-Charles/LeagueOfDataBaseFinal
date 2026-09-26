using LoDb.Api.Modules.Accounts.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.Session;

/// <summary>
/// <c>GET /api/account/me</c>: the session of the caller, and the XSRF token of the web
/// front, which calls it before its first unsafe request.
/// </summary>
internal sealed class MeEndpoint(SessionReader sessions, XsrfTokens xsrf)
{
    // The answer depends on the cookies: no cache may keep it.
    private const string NoStore = "no-store";

    public static void Map(IEndpointRouteBuilder account) =>
        account.MapGet(
                "/me",
                static ([FromServices] MeEndpoint endpoint, HttpContext context) =>
                    endpoint.GetAsync(context))
            .WithName("getAccountSession")
            .WithSummary("The signed-in account, if any, with a fresh XSRF token cookie.");

    public async Task<Ok<AccountSession>> GetAsync(HttpContext context)
    {
        // The apps authenticate by bearer token: no forgery to guard against.
        if (!AccountSchemes.IsBearer(context.Request))
        {
            xsrf.Issue(context);
        }

        context.Response.Headers.CacheControl = NoStore;
        return TypedResults.Ok(await sessions.ReadAsync(context.User));
    }
}
