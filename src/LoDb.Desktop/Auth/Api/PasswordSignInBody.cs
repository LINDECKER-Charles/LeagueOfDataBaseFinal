namespace LoDb.Desktop.Auth.Api;

/// <summary>Body of <c>POST /api/account/token</c>.</summary>
internal sealed record PasswordSignInBody
{
    public required string? Identifier { get; init; }

    public required string? Password { get; init; }

    public string? TwoFactorCode { get; init; }

    public string? RecoveryCode { get; init; }
}
