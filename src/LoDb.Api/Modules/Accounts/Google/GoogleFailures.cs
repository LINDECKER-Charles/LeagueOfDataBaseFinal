namespace LoDb.Api.Modules.Accounts.Google;

/// <summary>
/// Why a Google sign-in failed: the <c>error</c> of the login page the web flow returns to,
/// and the <c>code</c> of the problem the app exchange answers with.
/// </summary>
internal static class GoogleFailures
{
    /// <summary>Google sign-in is not configured, or Google could not be reached.</summary>
    public const string Unavailable = "google-unavailable";

    /// <summary>The visitor declined to share their Google profile.</summary>
    public const string Cancelled = "google-cancelled";

    /// <summary>Google refused, or the account could not be tied to the Google identity.</summary>
    public const string Failed = "google-failed";

    /// <summary>
    /// Google does not vouch for the e-mail, and an account already uses it: linking would
    /// hand that account to whoever typed the address at Google.
    /// </summary>
    public const string EmailUnverified = "google-email-unverified";

    public const string AccountBanned = "account-banned";

    public const string AccountLocked = "account-locked";

    /// <summary>
    /// The account has a second factor, which a Google sign-in cannot provide: it signs in
    /// with its password and its code.
    /// </summary>
    public const string TwoFactorRequired = "two-factor-required";
}
