using LoDb.Api.Modules.ClientPolicy.Policy;
using LoDb.Api.Modules.ClientPolicy.Versions;
using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Modules.ClientPolicy.Gate;

/// <summary>
/// The <c>426 Upgrade Required</c> of an app below the minimum version: a ProblemDetails
/// whose <c>code</c> the apps switch on, with what they need for the blocking screen.
/// </summary>
/// <remarks>
/// RFC 9110 pairs a 426 with an <c>Upgrade</c> header naming a protocol; the upgrade here is
/// the app's, not the protocol's, and HTTP/2 forbids that header, so none is sent.
/// </remarks>
internal static class UpgradeRequiredProblem
{
    public const string Code = "client-upgrade-required";

    private const string Title = "This version of the app is no longer supported.";
    private const string NoStore = "no-store";

    public static Task WriteAsync(HttpContext httpContext, AppClient client, PlatformPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(policy);

        // The answer depends on a header: no cache may hand it to another app.
        httpContext.Response.Headers[HeaderNames.CacheControl] = NoStore;
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = Code,
            ["platform"] = ClientPlatforms.NameOf(client.Platform),
            ["clientVersion"] = client.Version.ToString(),
            ["minimumVersion"] = policy.MinimumVersion,
            ["latestVersion"] = policy.LatestVersion,
        };
        return TypedResults
            .Problem(
                statusCode: StatusCodes.Status426UpgradeRequired,
                title: Title,
                extensions: extensions)
            .ExecuteAsync(httpContext);
    }
}
