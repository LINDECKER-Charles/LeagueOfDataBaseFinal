namespace LoDb.Desktop.Auth.Api;

/// <summary>
/// Token response of the API's app sign-ins (<c>/api/account/token</c>, <c>/refresh</c>,
/// <c>/google/app/exchange</c>, L4.2): Identity's bearer token response.
/// </summary>
internal sealed record TokenResponse
{
    public required string AccessToken { get; init; }

    /// <summary>Lifetime of the access token, in seconds.</summary>
    public required long ExpiresIn { get; init; }

    public required string RefreshToken { get; init; }

    /// <summary>A response the session can hold: both tokens and a positive lifetime.</summary>
    public bool IsComplete =>
        !string.IsNullOrEmpty(AccessToken)
        && !string.IsNullOrEmpty(RefreshToken)
        && ExpiresIn > 0;
}
