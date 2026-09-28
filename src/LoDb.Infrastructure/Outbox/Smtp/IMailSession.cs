using MimeKit;

namespace LoDb.Infrastructure.Outbox.Smtp;

/// <summary>A connection to the relay, reused by the messages of a batch.</summary>
internal interface IMailSession : IAsyncDisposable
{
    /// <summary>Sends one message; a failure throws and leaves the session usable.</summary>
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);
}
