using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Outbox.Smtp;

/// <summary>Sends through the relay of <see cref="MailOptions"/> with MailKit.</summary>
internal sealed class SmtpMailTransport(IOptions<MailOptions> options) : IMailTransport
{
    public IMailSession OpenSession() => new SmtpMailSession(options.Value);
}
