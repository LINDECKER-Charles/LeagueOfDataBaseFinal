using System.Collections.Concurrent;
using LoDb.Infrastructure.Outbox;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>The e-mail outbox of lot L4.3, replaced by the list of what is queued.</summary>
/// <remarks>
/// The real outbox writes with the caller's next save; this one records at once, which the
/// accounts never tell apart: they save right after queuing.
/// </remarks>
public sealed class RecordingOutbox : IEmailOutbox
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyList<EmailMessage> Messages => [.. _messages];

    public Task EnqueueAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>The e-mails queued with <paramref name="template"/>, oldest first.</summary>
    public IReadOnlyList<EmailMessage> Sent(EmailTemplate template) =>
        [.. _messages.Where(message => message.Template == template)];

    /// <summary>The link of the last e-mail queued with <paramref name="template"/>.</summary>
    public EmailLink LastLink(EmailTemplate template) => EmailLink.Of(Sent(template)[^1]);

    /// <summary>The e-mails queued for <paramref name="recipient"/>, oldest first.</summary>
    public IReadOnlyList<EmailMessage> To(string recipient) =>
        [.. _messages.Where(message => message.Recipient == recipient)];

    /// <summary>
    /// The link of the last e-mail queued for <paramref name="recipient"/> with
    /// <paramref name="template"/>.
    /// </summary>
    public EmailLink LastLink(string recipient, EmailTemplate template) =>
        EmailLink.Of(To(recipient).Last(message => message.Template == template));
}
