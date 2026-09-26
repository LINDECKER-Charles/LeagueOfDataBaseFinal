using LoDb.Infrastructure.Outbox.Rendering;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LoDb.Infrastructure.Outbox.Smtp;

/// <summary>
/// Addresses a rendered e-mail: the configured sender, the queued recipient, the Reply-To of
/// the template, and an HTML part with its text alternative.
/// </summary>
internal sealed class EmailComposer(IOptions<MailOptions> options)
{
    public MimeMessage Compose(string recipient, RenderedEmail email)
    {
        var to = MailboxAddress.Parse(recipient);
        to.Name = email.RecipientName;
        var message = new MimeMessage
        {
            Subject = email.Subject,
            Body = new BodyBuilder { HtmlBody = email.Html, TextBody = email.Text }.ToMessageBody(),
        };
        message.From.Add(MailboxAddress.Parse(options.Value.From));
        message.To.Add(to);
        if (email.ReplyTo is not null)
        {
            message.ReplyTo.Add(email.ReplyTo);
        }

        return message;
    }
}
