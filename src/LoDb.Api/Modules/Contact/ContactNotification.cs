using System.Globalization;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence.Contact;

namespace LoDb.Api.Modules.Contact;

/// <summary>
/// The e-mail that forwards a stored message to the team, the visitor as its Reply-To
/// (<c>ContactEmailView</c>).
/// </summary>
internal static class ContactNotification
{
    // ISO 8601 with its offset, as the template's model requires.
    private const string RoundTrip = "O";

    public static EmailMessage Of(ContactMessage message, ContactOptions options)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);
        return new EmailMessage
        {
            Recipient = Recipient(options)
                ?? throw new InvalidOperationException("No contact recipient is set."),
            Template = EmailTemplate.ContactNotification,
            Locale = options.NotificationLocale,
            Model = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [EmailModelKeys.ContactCategory] = message.Category,
                [EmailModelKeys.ContactName] = message.Name,
                [EmailModelKeys.ContactEmail] = message.Email,
                [EmailModelKeys.ContactSubject] = message.Subject,
                [EmailModelKeys.ContactMessage] = message.Message,
                [EmailModelKeys.ContactReceivedAt] =
                    message.CreatedAt.ToString(RoundTrip, CultureInfo.InvariantCulture),
                [EmailModelKeys.ContactLocale] = message.Locale,
            },
        };
    }

    /// <summary>The mailbox to forward to; null when the messages stay in the inbox.</summary>
    public static string? Recipient(ContactOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return string.IsNullOrWhiteSpace(options.Recipient) ? null : options.Recipient.Trim();
    }
}
