namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>How many e-mails of a kind an account may ask for over a sliding window.</summary>
/// <param name="Name">Name of the row that counts them.</param>
/// <param name="Limit">E-mails within the window.</param>
/// <param name="Window">Length of the window.</param>
internal sealed record MailThrottleRule(string Name, int Limit, TimeSpan Window)
{
    /// <summary>Verification e-mails asked again: 3 per 15 minutes, as the legacy stack.</summary>
    public static MailThrottleRule ConfirmEmail { get; } =
        new(Name: "confirm-email-requests", Limit: 3, Window: TimeSpan.FromMinutes(15));

    /// <summary>Password reset e-mails: 1 an hour, the lifetime of the link.</summary>
    public static MailThrottleRule ResetPassword { get; } =
        new(Name: "reset-password-requests", Limit: 1, Window: TimeSpan.FromHours(1));
}
