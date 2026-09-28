using System.Net;
using Microsoft.Net.Http.Headers;

namespace LoDb.Desktop.Auth.Google;

/// <summary>
/// The page the system browser shows at the end of a Google sign-in: the app itself goes
/// on in its window, which learns the outcome from <c>/desktop/auth/session</c>.
/// </summary>
internal static class GoogleCallbackPage
{
    private const string HtmlContentType = "text/html; charset=utf-8";
    private const string NoStore = "no-store";

    // No script, no external resource: the page must leak nothing of the code in its URL.
    private const string Policy = "default-src 'none'; style-src 'unsafe-inline'";
    private const string NoReferrer = "no-referrer";
    private const string SecurityPolicyHeader = "Content-Security-Policy";
    private const string ReferrerPolicyHeader = "Referrer-Policy";

    private const string SignedIn =
        "Connexion réussie : vous pouvez revenir à League of Data Base."
        + " / Signed in: you can go back to League of Data Base.";

    private const string Failed =
        "La connexion n'a pas abouti ; recommencez depuis l'application."
        + " / Sign-in failed; please start again from the app.";

    public static IResult For(string? failure, HttpResponse response)
    {
        response.Headers[HeaderNames.CacheControl] = NoStore;
        response.Headers[SecurityPolicyHeader] = Policy;
        response.Headers[ReferrerPolicyHeader] = NoReferrer;
        var message = failure is null ? SignedIn : Failed;
        var status = failure == GoogleFailures.InvalidState
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status200OK;
        return TypedResults.Content(Html(message), HtmlContentType, statusCode: status);
    }

    private static string Html(string message) =>
        "<!doctype html><html><head><meta charset=\"utf-8\"><title>League of Data Base</title>"
        + "<style>body{font-family:system-ui,sans-serif;margin:4rem auto;max-width:36rem;"
        + "padding:0 1rem;line-height:1.5}</style></head><body><p>"
        + WebUtility.HtmlEncode(message)
        + "</p></body></html>";
}
