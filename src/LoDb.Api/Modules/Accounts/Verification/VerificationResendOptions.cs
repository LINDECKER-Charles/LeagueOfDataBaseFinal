using System.ComponentModel.DataAnnotations;

namespace LoDb.Api.Modules.Accounts.Verification;

/// <summary>
/// How often an account may ask for its verification e-mail again
/// (<c>LoDb:Accounts:VerificationResend</c>), checked when the host starts.
/// </summary>
/// <remarks>
/// Counted by account, never by address: the request needs a session (ADR 0009). The e-mail
/// of the registration itself is not a resend.
/// </remarks>
internal sealed class VerificationResendOptions
{
    public const string SectionName = "LoDb:Accounts:VerificationResend";

    private const int DefaultFreeResends = 5;
    private const int MostFreeResends = 100;
    private const int DefaultIntervalHours = 24;

    private static readonly TimeSpan ShortestInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LongestInterval = TimeSpan.FromDays(7);

    /// <summary>Resends over the life of the account, at any pace.</summary>
    [Range(0, MostFreeResends)]
    public int FreeResends { get; set; } = DefaultFreeResends;

    /// <summary>The wait between two resends once those are spent.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(DefaultIntervalHours);

    /// <summary>Whether the interval is one, which a range cannot state.</summary>
    public bool HasUsableInterval() => Interval >= ShortestInterval && Interval <= LongestInterval;
}
