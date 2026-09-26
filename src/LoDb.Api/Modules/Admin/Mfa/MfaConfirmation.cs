using LoDb.Api.Modules.Accounts.Session;

namespace LoDb.Api.Modules.Admin.Mfa;

/// <summary>
/// An authenticator turned on: the recovery codes, shown this once, and the session, now
/// opened with the second factor.
/// </summary>
internal sealed record MfaConfirmation
{
    /// <summary>Single-use codes that replace the authenticator at sign-in.</summary>
    public required IReadOnlyList<string> RecoveryCodes { get; init; }

    public required AccountSession Session { get; init; }
}
