using LoDb.Api.Modules.Accounts.Links;
using LoDb.Domain.Languages;
using Microsoft.AspNetCore.Authentication;

namespace LoDb.Api.Modules.Accounts.Google.Web;

/// <summary>
/// The end of a Google sign-in on the web. Google sends the browser back to
/// <c>/api/account/google/callback</c>, which goes on to the page the sign-in started from,
/// or to the login page with the <c>error</c> of the failure.
/// </summary>
internal sealed class GoogleCallback(GoogleSignIn googleSignIn)
{
    /// <summary>Item of the authentication properties: the locale of the front pages.</summary>
    public const string LocaleItem = "lodb.locale";

    // Google's code of a consent the visitor declined, which the handler keeps in Data.
    private const string ErrorData = "error";
    private const string AccessDenied = "access_denied";

    /// <summary>Signs the account in, in place of the handler's external cookie.</summary>
    public async Task CompleteAsync(TicketReceivedContext context)
    {
        var locale = LocaleOf(context.Properties);
        var profile = context.Principal is { } principal ? GoogleProfile.From(principal) : null;
        var failure = profile is null
            ? GoogleFailures.Failed
            : await googleSignIn.SignInWebAsync(
                profile,
                context.Properties?.IsPersistent == true,
                context.HttpContext.RequestAborted);
        context.Response.Redirect(failure is null
            ? context.ReturnUri ?? FrontPages.Path(locale, FrontPages.Profile)
            : FrontPages.LoginFailed(locale, failure));
        context.HandleResponse();
    }

    /// <summary>Sends the browser to the login page when Google or the handler failed.</summary>
    public static Task FailAsync(RemoteFailureContext context)
    {
        var declined = context.Failure?.Data[ErrorData] as string == AccessDenied;
        var failure = declined ? GoogleFailures.Cancelled : GoogleFailures.Failed;
        context.Response.Redirect(FrontPages.LoginFailed(LocaleOf(context.Properties), failure));
        context.HandleResponse();
        return Task.CompletedTask;
    }

    private static UiLocale LocaleOf(AuthenticationProperties? properties) =>
        properties?.Items.TryGetValue(LocaleItem, out var code) == true
        && UiLocales.TryParse(code, out var locale)
            ? locale
            : UiLocales.Fallback;
}
