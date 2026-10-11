namespace LoDb.Infrastructure.Audience.Umami;

/// <summary>
/// Umami refused a request of the pull, or answered what it does not answer: a status other
/// than success, a missing token. Never carries the account's credentials nor its token.
/// </summary>
internal sealed class UmamiException(string message) : Exception(message);
