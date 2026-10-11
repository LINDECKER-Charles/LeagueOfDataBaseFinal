namespace LoDb.Infrastructure.Outbox;

/// <summary>
/// E-mails of each template the whole site sends in a sliding hour
/// (<c>LoDb:Outbox:HourlyCaps</c>); the next ones wait in the table for the hour to free a
/// place, never dropped.
/// </summary>
/// <remarks>
/// <para>
/// Sized far above the busiest hour a fan site sees, they only bind when a script creates
/// accounts or asks for links in bulk: the relay, whose reputation every e-mail of the site
/// shares, then never sends more than their sum in an hour, and a flood of one template
/// leaves the others their own budget.
/// </para>
/// <para>
/// A link held back longer than its lifetime arrives expired: the member asks for another.
/// </para>
/// </remarks>
public sealed class OutboxHourlyCaps
{
    /// <summary>Above this, a cap bounds nothing a relay would accept.</summary>
    public const int MostPerHour = 1_000_000;

    private const int DefaultConfirmEmails = 100;
    private const int DefaultPasswordResets = 50;
    private const int DefaultContactNotifications = 20;

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    /// <summary>The sliding window the caps count over.</summary>
    public static TimeSpan Window => Hour;

    /// <summary>Verification links, at registration or asked again.</summary>
    public int ConfirmEmail { get; set; } = DefaultConfirmEmails;

    /// <summary>Password reset links.</summary>
    public int ResetPassword { get; set; } = DefaultPasswordResets;

    /// <summary>Contact messages forwarded to the site's team, whose inbox they fill.</summary>
    public int ContactNotification { get; set; } = DefaultContactNotifications;

    /// <summary>The cap of <paramref name="template"/>.</summary>
    public int Of(EmailTemplate template) => template switch
    {
        EmailTemplate.ConfirmEmail => ConfirmEmail,
        EmailTemplate.ResetPassword => ResetPassword,
        EmailTemplate.ContactNotification => ContactNotification,
        _ => throw new ArgumentOutOfRangeException(nameof(template), template, null),
    };
}
