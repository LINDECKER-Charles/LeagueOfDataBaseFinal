using LoDb.Api.Modules.Accounts.Verification;

namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>
/// How many e-mails of a kind an account may ask for: a first allowance over the life of the
/// account, at any pace, then <see cref="Limit"/> per sliding <see cref="Window"/>.
/// </summary>
internal sealed record MailThrottleRule
{
    private const int OnePerWindow = 1;

    /// <summary>Name of the row that counts them.</summary>
    public required string Name { get; init; }

    /// <summary>E-mails asked for before the window applies; none by default.</summary>
    public int Allowance { get; init; }

    /// <summary>E-mails within the window, once the allowance is spent.</summary>
    public required int Limit { get; init; }

    /// <summary>Length of the window.</summary>
    public required TimeSpan Window { get; init; }

    /// <summary>Password reset e-mails: 1 an hour, the lifetime of the link.</summary>
    public static MailThrottleRule ResetPassword { get; } = new()
    {
        Name = "reset-password-requests",
        Limit = OnePerWindow,
        Window = TimeSpan.FromHours(1),
    };

    /// <summary>
    /// Verification e-mails asked again: a few in all, 5 by default, then one per interval, a
    /// day by default. An unverified account is erased after a week, so an address typed by
    /// someone else receives a handful of them at most.
    /// </summary>
    public static MailThrottleRule ConfirmEmail(VerificationResendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new MailThrottleRule
        {
            Name = "confirm-email-requests",
            Allowance = options.FreeResends,
            Limit = OnePerWindow,
            Window = options.Interval,
        };
    }
}
