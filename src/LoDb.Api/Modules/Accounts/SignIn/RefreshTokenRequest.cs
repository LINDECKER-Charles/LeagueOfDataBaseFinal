namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>Body of <c>POST /api/account/refresh</c>.</summary>
internal sealed record RefreshTokenRequest
{
    /// <summary>The refresh token of the last token response.</summary>
    public required string? RefreshToken { get; init; }
}
