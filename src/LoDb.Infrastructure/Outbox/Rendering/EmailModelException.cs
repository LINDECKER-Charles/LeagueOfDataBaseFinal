namespace LoDb.Infrastructure.Outbox.Rendering;

/// <summary>
/// The stored model cannot fill its template: another attempt would fail the same way, so
/// the message is dead at once.
/// </summary>
/// <remarks>The message names the key, never its value, which may be personal.</remarks>
internal sealed class EmailModelException(string message) : Exception(message);
