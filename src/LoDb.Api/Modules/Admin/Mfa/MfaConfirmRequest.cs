namespace LoDb.Api.Modules.Admin.Mfa;

/// <summary>A code of the authenticator just set up, proving it holds the key.</summary>
internal sealed record MfaConfirmRequest
{
    /// <summary>Six digits, spaces and dashes allowed between them.</summary>
    public string? Code { get; init; }
}
