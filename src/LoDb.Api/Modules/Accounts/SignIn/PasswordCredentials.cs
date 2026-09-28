namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>What a sign-in with a password carries, from the web form or from an app.</summary>
internal sealed record PasswordCredentials
{
    /// <summary>The e-mail or the username, whatever the case.</summary>
    public required string? Identifier { get; init; }

    public required string? Password { get; init; }

    /// <summary>The code of the authenticator app, for an account with a second factor.</summary>
    public string? TwoFactorCode { get; init; }

    /// <summary>A recovery code, in place of the authenticator's.</summary>
    public string? RecoveryCode { get; init; }

    /// <summary>Keeps the session for 30 days rather than until the browser closes.</summary>
    public bool RememberMe { get; init; }
}
