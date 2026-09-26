namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>
/// A message this instance took, as stored: the template and the locale stay text until
/// the message is sent, so that one unreadable row fails alone. <c>Attempts</c> counts the
/// attempts so far, this one included.
/// </summary>
internal sealed record ClaimedMessage(
    long Id,
    string Recipient,
    string Template,
    string Locale,
    string Model,
    int Attempts);
