namespace LoDb.Api.Modules.Admin.Mfa;

/// <summary>What opens the enrolment of an authenticator.</summary>
internal sealed record MfaEnrollmentRequest
{
    /// <summary>
    /// The invitation <c>admin mfa-invite</c> printed for the account, valid a few minutes.
    /// </summary>
    public string? Invitation { get; init; }
}
