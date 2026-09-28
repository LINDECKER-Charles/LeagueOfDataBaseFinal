using System.Text.Json;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Outbox;
using MimeKit;

namespace LoDb.Infrastructure.Outbox;

/// <summary>
/// Adds each message to the caller's context as a pending row, due at once: the caller's
/// <c>SaveChangesAsync</c> writes it with the change that asks for it, or not at all.
/// </summary>
internal sealed class EmailOutbox(LoDbDbContext context, TimeProvider timeProvider) : IEmailOutbox
{
    private const int RecipientMaxLength = 180;

    public Task EnqueueAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        CheckRecipient(message.Recipient);
        if (!Enum.IsDefined(message.Template) || !Enum.IsDefined(message.Locale))
        {
            throw new ArgumentOutOfRangeException(
                nameof(message),
                "The template and the locale must be defined values.");
        }

        var now = timeProvider.GetUtcNow();
        context.EmailOutbox.Add(new EmailOutboxMessage
        {
            Recipient = message.Recipient,
            Template = message.Template,
            Locale = message.Locale,
            Model = JsonSerializer.Serialize(message.Model),
            Status = EmailOutboxStatus.Pending,
            Attempts = 0,
            NextAttemptAt = now,
            CreatedAt = now,
        });
        return Task.CompletedTask;
    }

    // Refused here rather than dead later: the caller still knows which input was wrong.
    // The message names no address, since exceptions end up in the logs.
    private static void CheckRecipient(string recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient)
            || recipient.Length > RecipientMaxLength
            || !MailboxAddress.TryParse(recipient, out _))
        {
            throw new ArgumentException(
                $"The recipient must be one address of at most {RecipientMaxLength} characters.",
                nameof(recipient));
        }
    }
}
