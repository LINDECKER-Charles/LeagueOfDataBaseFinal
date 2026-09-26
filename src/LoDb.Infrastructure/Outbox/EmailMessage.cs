using LoDb.Domain.Languages;

namespace LoDb.Infrastructure.Outbox;

/// <summary>An e-mail to send once the change that asks for it is committed.</summary>
public sealed record EmailMessage
{
    /// <summary>Address the e-mail goes to; it is never written to the logs.</summary>
    public required string Recipient { get; init; }

    public required EmailTemplate Template { get; init; }

    /// <summary>
    /// Locale of the recipient: the e-mail never follows the locale of the request that
    /// sends it.
    /// </summary>
    public required UiLocale Locale { get; init; }

    /// <summary>Values the template fills in (links, names), stored as a JSON object.</summary>
    public required IReadOnlyDictionary<string, string?> Model { get; init; }
}
