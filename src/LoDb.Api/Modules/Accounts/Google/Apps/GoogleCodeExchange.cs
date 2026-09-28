using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Google.Apps;

/// <summary>
/// Redeems the code of an app's Google sign-in at Google's token endpoint, then reads the
/// profile at the userinfo endpoint, through the backchannel of the web handler.
/// </summary>
/// <remarks>
/// The userinfo claims are mapped by the web handler's own claim actions, so that an app's
/// profile reads exactly as a web one.
/// </remarks>
internal sealed partial class GoogleCodeExchange(
    IOptionsMonitor<GoogleOptions> googleOptions,
    ILogger<GoogleCodeExchange> logger)
{
    private const string GrantTypeKey = "grant_type";
    private const string AuthorizationCode = "authorization_code";
    private const string CodeKey = "code";
    private const string RedirectUriKey = "redirect_uri";
    private const string ClientIdKey = "client_id";
    private const string ClientSecretKey = "client_secret";
    private const string AccessTokenKey = "access_token";
    private const string BearerScheme = "Bearer";

    /// <summary>The Google profile of the code; null when Google refuses it.</summary>
    /// <exception cref="HttpRequestException">Google could not be reached.</exception>
    public async Task<GoogleProfile?> ExchangeAsync(
        GoogleAppClient client,
        GoogleExchangeRequest request,
        CancellationToken cancellationToken)
    {
        var options = googleOptions.Get(GoogleDefaults.AuthenticationScheme);
        using var form = new FormUrlEncodedContent(Form(client, request));
        using var redeemed = await options.Backchannel.PostAsync(
            options.TokenEndpoint,
            form,
            cancellationToken);
        if (!redeemed.IsSuccessStatusCode)
        {
            LogCodeRefused(logger, (int)redeemed.StatusCode);
            return null;
        }

        using var tokens = await ReadJsonAsync(redeemed, cancellationToken);
        return tokens?.RootElement.TryGetProperty(AccessTokenKey, out var accessToken) == true
            && accessToken.GetString() is { Length: > 0 } bearer
                ? await ReadProfileAsync(options, bearer, cancellationToken)
                : null;
    }

    private static Dictionary<string, string> Form(
        GoogleAppClient client,
        GoogleExchangeRequest request)
    {
        var form = new Dictionary<string, string>
        {
            [GrantTypeKey] = AuthorizationCode,
            [CodeKey] = request.Code ?? string.Empty,
            [OAuthConstants.CodeVerifierKey] = request.CodeVerifier ?? string.Empty,
            [RedirectUriKey] = request.RedirectUri ?? string.Empty,
            [ClientIdKey] = client.ClientId ?? string.Empty,
        };
        if (!string.IsNullOrEmpty(client.ClientSecret))
        {
            form[ClientSecretKey] = client.ClientSecret;
        }

        return form;
    }

    private static async Task<GoogleProfile?> ReadProfileAsync(
        GoogleOptions options,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, options.UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, accessToken);
        using var response = await options.Backchannel.SendAsync(request, cancellationToken);
        using var user = response.IsSuccessStatusCode
            ? await ReadJsonAsync(response, cancellationToken)
            : null;
        if (user?.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var identity = new ClaimsIdentity(GoogleDefaults.AuthenticationScheme);
        var issuer = options.ClaimsIssuer ?? GoogleDefaults.AuthenticationScheme;
        foreach (var action in options.ClaimActions)
        {
            action.Run(user.RootElement, identity, issuer);
        }

        return GoogleProfile.From(new ClaimsPrincipal(identity));
    }

    private static async Task<JsonDocument?> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(
        EventName = "accounts.google.code_refused",
        Level = LogLevel.Information,
        Message = "Google refused the code of an app sign-in with status {Status}.")]
    private static partial void LogCodeRefused(ILogger logger, int status);
}
