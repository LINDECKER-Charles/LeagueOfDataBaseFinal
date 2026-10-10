namespace LoDb.Api.Modules.Accounts.Security.Invitations;

/// <summary>
/// The invitation an operator hands to an administrator to enrol an authenticator
/// (<c>LoDb:Accounts:MfaInvitation</c>), checked when the host starts.
/// </summary>
internal sealed class MfaInvitationOptions
{
    public const string SectionName = "LoDb:Accounts:MfaInvitation";

    private const int DefaultLifetimeMinutes = 15;

    // Long enough to sign in and scan a code; the command prints another one at will.
    private static readonly TimeSpan LongestLifetime = TimeSpan.FromHours(1);

    /// <summary>How long an invitation opens the enrolment after the command printed it.</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(DefaultLifetimeMinutes);

    /// <summary>Whether the lifetime is positive and short, which a range cannot state.</summary>
    public bool HasUsableLifetime() => Lifetime > TimeSpan.Zero && Lifetime <= LongestLifetime;
}
