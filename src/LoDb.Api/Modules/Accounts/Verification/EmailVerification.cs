namespace LoDb.Api.Modules.Accounts.Verification;

/// <summary>A verified e-mail.</summary>
internal sealed record EmailVerification
{
    /// <summary>
    /// Whether an earlier click of the link verified it: a link stays valid for its hour.
    /// </summary>
    public required bool AlreadyVerified { get; init; }
}
