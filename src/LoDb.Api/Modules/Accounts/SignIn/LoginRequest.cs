namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>Body of <c>POST /api/account/login</c>.</summary>
internal sealed record LoginRequest
{
    /// <summary>The e-mail or the username, whatever the case.</summary>
    public required string? Identifier { get; init; }

    public required string? Password { get; init; }

    /// <summary>Keeps the session for 30 days rather than until the browser closes.</summary>
    public bool RememberMe { get; init; }

    /// <summary>
    /// The code of the authenticator app, sent again with the password once a sign-in was
    /// answered <c>two-factor-required</c>.
    /// </summary>
    public string? TwoFactorCode { get; init; }

    /// <summary>A recovery code, in place of the authenticator's.</summary>
    public string? RecoveryCode { get; init; }

    public PasswordCredentials ToCredentials() => new()
    {
        Identifier = Identifier,
        Password = Password,
        TwoFactorCode = TwoFactorCode,
        RecoveryCode = RecoveryCode,
        RememberMe = RememberMe,
    };
}
