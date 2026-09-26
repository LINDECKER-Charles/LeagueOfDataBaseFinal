using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Security;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Google.Apps;

/// <summary>
/// <c>POST /api/account/google/app/exchange</c>: the Google sign-in of the apps. The app
/// signs in at Google in the system browser with PKCE, and hands the code here, which
/// redeems it and answers with the tokens of the account.
/// </summary>
internal sealed partial class GoogleExchangeEndpoint(
    IOptions<AccountsOptions> options,
    GoogleCodeExchange exchange,
    GoogleSignIn googleSignIn,
    ILogger<GoogleExchangeEndpoint> logger)
{
    /// <summary>Code of a <c>clientId</c> that names no configured Google client.</summary>
    public const string UnknownClient = "unknown-client";

    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/google/app/exchange",
                static (
                    [FromBody] GoogleExchangeRequest request,
                    [FromServices] GoogleExchangeEndpoint endpoint,
                    CancellationToken aborted) => endpoint.ExchangeAsync(request, aborted))
            .WithName("exchangeGoogleCode")
            .WithSummary("Signs an app in with the code of a Google sign-in, redeemed here.")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<EmptyHttpResult, AccountProblem>> ExchangeAsync(
        GoogleExchangeRequest request,
        CancellationToken cancellationToken)
    {
        var google = options.Value.Google;
        if (!google.IsEnabled)
        {
            return AccountProblem.GoogleUnavailable();
        }

        var client = ClientOf(google, request.ClientId);
        var errors = Check(request, client);
        if (!errors.IsEmpty)
        {
            return errors.ToProblem();
        }

        var (reached, profile) = await RedeemAsync(client!, request, cancellationToken);
        if (profile is null)
        {
            return reached
                ? AccountProblem.GoogleExchangeFailed()
                : AccountProblem.GoogleUnavailable();
        }

        // On success the bearer handler has written the token response.
        var failure = await googleSignIn.SignInAppAsync(profile, cancellationToken);
        return failure is null ? TypedResults.Empty : Refusal(failure);
    }

    private async Task<(bool Reached, GoogleProfile? Profile)> RedeemAsync(
        GoogleAppClient client,
        GoogleExchangeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return (true, await exchange.ExchangeAsync(client, request, cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            LogGoogleUnreachable(logger, exception);
            return (false, null);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The backchannel timed out.
            LogGoogleUnreachable(logger, exception);
            return (false, null);
        }
    }

    // The web client when the app names none, else one of the configured app clients.
    private static GoogleAppClient? ClientOf(GoogleAccountOptions google, string? clientId) =>
        string.IsNullOrEmpty(clientId) || clientId == google.ClientId
            ? new GoogleAppClient { ClientId = google.ClientId, ClientSecret = google.ClientSecret }
            : google.AppClients.FirstOrDefault(app => app.ClientId == clientId);

    private static FieldErrors Check(GoogleExchangeRequest request, GoogleAppClient? client)
    {
        var errors = new FieldErrors();
        errors.RequireText(AccountFields.Code, request.Code);
        errors.RequireText(AccountFields.CodeVerifier, request.CodeVerifier);
        errors.RequireText(AccountFields.RedirectUri, request.RedirectUri);
        if (client is null)
        {
            errors.Add(AccountFields.ClientId, UnknownClient);
        }

        return errors;
    }

    private static AccountProblem Refusal(string failure) => failure switch
    {
        GoogleFailures.EmailUnverified => AccountProblem.GoogleEmailUnverified(),
        GoogleFailures.AccountBanned => AccountProblem.AccountBanned(),
        GoogleFailures.AccountLocked =>
            AccountProblem.AccountLocked(AccountsSecurityRegistration.LockoutDuration),
        GoogleFailures.TwoFactorRequired => AccountProblem.TwoFactorRequired(),
        _ => AccountProblem.GoogleFailed(),
    };

    [LoggerMessage(
        EventName = "accounts.google.unreachable",
        Level = LogLevel.Warning,
        Message = "Google could not be reached to redeem the code of an app sign-in.")]
    private static partial void LogGoogleUnreachable(ILogger logger, Exception exception);
}
