namespace LoDb.Desktop.Auth.Api;

/// <summary>Body of <c>POST /api/account/google/app/exchange</c>.</summary>
internal sealed record GoogleExchangeBody
{
    public required string Code { get; init; }

    public required string CodeVerifier { get; init; }

    /// <summary>The loopback redirect URI, exactly as sent to Google.</summary>
    public required string RedirectUri { get; init; }

    /// <summary>The "desktop app" client the code was issued to.</summary>
    public required string ClientId { get; init; }
}
