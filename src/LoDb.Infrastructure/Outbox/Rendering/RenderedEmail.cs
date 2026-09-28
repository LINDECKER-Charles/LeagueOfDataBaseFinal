using MimeKit;

namespace LoDb.Infrastructure.Outbox.Rendering;

/// <summary>An e-mail written in its recipient's locale, ready to address and send.</summary>
internal sealed record RenderedEmail
{
    public required string Subject { get; init; }

    public required string Html { get; init; }

    public required string Text { get; init; }

    /// <summary>Display name of the recipient, when the model gives one.</summary>
    public string? RecipientName { get; init; }

    /// <summary>Where a reply goes, when it is not the sender.</summary>
    public MailboxAddress? ReplyTo { get; init; }
}
