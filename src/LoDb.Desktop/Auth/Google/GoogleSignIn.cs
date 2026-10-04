using LoDb.Desktop.Auth.Api;
using LoDb.Desktop.Auth.Tokens;
using LoDb.Desktop.Hosting;
using LoDb.Desktop.Shell;

namespace LoDb.Desktop.Auth.Google;

/// <summary>
/// Google sign-in of the desktop app (ADR 0009): Google's page in the system browser, as
/// Google refuses embedded WebViews, then the code exchanged by the API for our tokens.
/// </summary>
internal sealed partial class GoogleSignIn(
    DesktopOptions options,
    GoogleFlows flows,
    ISystemBrowser browser,
    AccountApiClient api,
    TokenSession session,
    ILogger<GoogleSignIn> logger)
{
    private const string AccessDenied = "access_denied";
    private const string SignedInOutcome = "signed-in";

    /// <returns>Null once the browser shows Google's page, the failure code otherwise.</returns>
    public string? Begin(Uri redirectUri, bool isRemembered)
    {
        if (options.GoogleClientId is not { } clientId)
        {
            return GoogleFailures.Unavailable;
        }

        var flow = flows.Begin(redirectUri, isRemembered);
        if (browser.Open(GoogleAuthorization.UrlFor(clientId, flow)))
        {
            return null;
        }

        flows.Abandon(flow, GoogleFailures.BrowserUnavailable);
        return GoogleFailures.BrowserUnavailable;
    }

    /// <returns>Null once the user is signed in, the failure code otherwise.</returns>
    public async Task<string?> CompleteAsync(
        GoogleCallbackQuery callback,
        CancellationToken cancellationToken)
    {
        if (flows.Take(callback.State) is not { } flow)
        {
            LogStateRejected(logger);
            return GoogleFailures.InvalidState;
        }

        var failure = callback switch
        {
            { Error: AccessDenied } => GoogleFailures.Cancelled,
            { Error: not null } or { Code: null or "" } => GoogleFailures.Denied,
            { Code: { } code } => await ExchangeAsync(flow, code, cancellationToken),
        };
        flows.End(failure is null ? GoogleStatus.Idle : GoogleStatus.FailedWith(failure));
        LogEnded(logger, failure ?? SignedInOutcome);
        return failure;
    }

    private async Task<string?> ExchangeAsync(
        GoogleFlow flow,
        string code,
        CancellationToken cancellationToken)
    {
        var call = await api.ExchangeGoogleAsync(
            new GoogleExchangeBody
            {
                Code = code,
                CodeVerifier = flow.Verifier,
                RedirectUri = flow.RedirectUri,
                ClientId = options.GoogleClientId!,
            },
            cancellationToken);
        if (call.Grant is { } grant)
        {
            await session.AdoptAsync(grant, flow.IsRemembered, cancellationToken);
            return null;
        }

        return call.Problem is { } problem
            ? problem.Code ?? GoogleFailures.ExchangeFailed
            : ProblemRelay.ApiUnreachable;
    }

    [LoggerMessage(
        EventName = "desktop.google.state_rejected",
        Level = LogLevel.Warning,
        Message = "A Google redirect carried a state of no running sign-in.")]
    private static partial void LogStateRejected(ILogger logger);

    [LoggerMessage(
        EventName = "desktop.google.ended",
        Level = LogLevel.Information,
        Message = "A Google sign-in ended: {Outcome}.")]
    private static partial void LogEnded(ILogger logger, string outcome);
}
