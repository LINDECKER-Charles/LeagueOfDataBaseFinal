using System.Globalization;
using MimeKit;

namespace LoDb.Infrastructure.Outbox.Rendering;

/// <summary>
/// The notification of a contact message (<c>contact.html</c>, <c>contact.txt</c>), sent to
/// the site's team with the visitor as Reply-To.
/// </summary>
/// <remarks>
/// The subject keeps the legacy shape, <c>[Contact · &lt;category&gt;] &lt;subject, else
/// name, else address&gt;</c>, which the team's mail filters may rely on.
/// </remarks>
internal static class ContactEmailView
{
    private const string NoName = "—";
    private const string ReceivedFormat = "dd/MM/yyyy HH:mm";

    public static RenderedEmail Render(LocalizedTexts texts, EmailModel model)
    {
        var name = model.Optional(EmailModelKeys.ContactName);
        var email = model.Required(EmailModelKeys.ContactEmail);
        var subject = model.Optional(EmailModelKeys.ContactSubject);
        var values = Labels(texts);
        values["category"] = Category(texts, model.Required(EmailModelKeys.ContactCategory));
        values["author"] = name ?? email;
        values["sender"] = name ?? NoName;
        values["email"] = email;
        values["subject"] = subject ?? string.Empty;
        values["receivedAt"] = ReceivedAt(model.Required(EmailModelKeys.ContactReceivedAt));
        values["visitorLocale"] = model.Optional(EmailModelKeys.ContactLocale) ?? string.Empty;
        values["message"] = model.Required(EmailModelKeys.ContactMessage);
        return new RenderedEmail
        {
            Subject = $"[Contact · {values["category"]}] {subject ?? name ?? email}",
            Html = TemplateEngine.Render(EmailResources.Template("contact.html"), values, true),
            Text = TemplateEngine.Render(EmailResources.Template("contact.txt"), values, false),
            ReplyTo = new MailboxAddress(name ?? string.Empty, email),
        };
    }

    private static Dictionary<string, string> Labels(LocalizedTexts texts) =>
        new(StringComparer.Ordinal)
        {
            ["lang"] = texts.Language,
            ["title"] = texts.Get("email.contact.title"),
            ["categoryLabel"] = texts.Get("email.contact.category"),
            ["senderLabel"] = texts.Get("email.contact.sender"),
            ["emailLabel"] = texts.Get("email.contact.email"),
            ["subjectLabel"] = texts.Get("email.contact.subject"),
            ["receivedLabel"] = texts.Get("email.contact.received"),
            ["replyLabel"] = texts.Get("email.contact.reply"),
            ["footer"] = texts.Get("email.contact.footer"),
        };

    // A code the texts do not know is shown as it is rather than lost.
    private static string Category(LocalizedTexts texts, string code) =>
        texts.TryGet($"email.contact.category.{code}") ?? code;

    private static string ReceivedAt(string value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var receivedAt)
            ? receivedAt.UtcDateTime.ToString(ReceivedFormat, CultureInfo.InvariantCulture)
            : throw new EmailModelException(
                $"{EmailModelKeys.ContactReceivedAt} is not an ISO 8601 time.");
}
