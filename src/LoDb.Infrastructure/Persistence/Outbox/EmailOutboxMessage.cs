using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;

namespace LoDb.Infrastructure.Persistence.Outbox;

/// <summary>A row of <c>email_outbox</c>: one e-mail and its delivery state.</summary>
/// <remarks>
/// The recipient and the model are personal data, and a model may hold a single-use link:
/// the anonymized copies empty the table.
/// </remarks>
public sealed class EmailOutboxMessage
{
    public long Id { get; set; }

    public required string Recipient { get; set; }

    public EmailTemplate Template { get; set; }

    public UiLocale Locale { get; set; }

    /// <summary>The values of the template, a JSON object.</summary>
    public required string Model { get; set; }

    public EmailOutboxStatus Status { get; set; }

    /// <summary>Delivery attempts made so far.</summary>
    public int Attempts { get; set; }

    /// <summary>Earliest time of the next attempt, the time of queuing for the first one.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Code of the last failure (SMTP status, exception type), not its text.</summary>
    public string? LastErrorCode { get; set; }
}
