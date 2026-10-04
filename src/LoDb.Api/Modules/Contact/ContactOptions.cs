using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Contact;

/// <summary>Settings of the contact form (<c>LoDb:Contact</c>).</summary>
internal sealed class ContactOptions
{
    public const string SectionName = "LoDb:Contact";

    /// <summary>
    /// Mailbox the messages are forwarded to, the legacy <c>CONTACT_RECIPIENT</c>. Unset or
    /// blank, the messages are only kept for the admin inbox.
    /// </summary>
    public string? Recipient { get; set; }

    /// <summary>
    /// Locale of the notification, the team's: an e-mail never follows the locale of the
    /// request that sends it.
    /// </summary>
    public UiLocale NotificationLocale { get; set; } = UiLocale.Fr;
}
