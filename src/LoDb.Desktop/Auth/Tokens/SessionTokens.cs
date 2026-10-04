namespace LoDb.Desktop.Auth.Tokens;

/// <summary>The tokens the host holds for the signed-in user.</summary>
internal sealed record SessionTokens
{
    /// <summary>Null after a restart, where only the refresh token was kept.</summary>
    public required string? AccessToken { get; init; }

    public required DateTimeOffset AccessExpiresAt { get; init; }

    public required string RefreshToken { get; init; }
}
