using LoDb.Desktop.Auth.Api;

namespace LoDb.Desktop.Auth;

/// <summary>Body of <c>POST /desktop/auth/login</c> (plan §5.2).</summary>
internal sealed record DesktopLoginRequest
{
    /// <summary>The e-mail or the username.</summary>
    public required string? Identifier { get; init; }

    public required string? Password { get; init; }

    /// <summary>Keeps the refresh token, encrypted, across restarts of the app.</summary>
    public bool RememberMe { get; init; }

    /// <summary>Sent again with the password after a <c>two-factor-required</c> answer.</summary>
    public string? TwoFactorCode { get; init; }

    public string? RecoveryCode { get; init; }

    public PasswordSignInBody ToBody() => new()
    {
        Identifier = Identifier,
        Password = Password,
        TwoFactorCode = TwoFactorCode,
        RecoveryCode = RecoveryCode,
    };
}
