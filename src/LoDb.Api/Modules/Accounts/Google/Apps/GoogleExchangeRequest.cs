namespace LoDb.Api.Modules.Accounts.Google.Apps;

/// <summary>
/// Body of <c>POST /api/account/google/app/exchange</c>: what an app got from a Google
/// sign-in in the system browser, with PKCE.
/// </summary>
internal sealed record GoogleExchangeRequest
{
    /// <summary>The authorization code Google handed to the redirect URI.</summary>
    public required string? Code { get; init; }

    /// <summary>The PKCE verifier whose challenge the app sent to Google.</summary>
    public required string? CodeVerifier { get; init; }

    /// <summary>The redirect URI the app sent to Google, as sent.</summary>
    public required string? RedirectUri { get; init; }

    /// <summary>The Google client the app signed in with; the web client when unset.</summary>
    public string? ClientId { get; init; }
}
