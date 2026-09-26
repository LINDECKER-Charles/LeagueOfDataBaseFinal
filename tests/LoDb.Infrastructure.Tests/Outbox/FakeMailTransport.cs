using System.Collections.Concurrent;
using LoDb.Infrastructure.Outbox.Smtp;
using MimeKit;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// A relay that keeps what it is sent, and fails the next sends with the exceptions queued
/// in <see cref="Failures"/>. Safe for several dispatchers at once.
/// </summary>
internal sealed class FakeMailTransport : IMailTransport
{
    private int _attempts;

    public ConcurrentQueue<Exception> Failures { get; } = new();

    public ConcurrentQueue<MimeMessage> Sent { get; } = new();

    /// <summary>Fails every send when set, whatever <see cref="Failures"/> holds.</summary>
    public Exception? AlwaysFail { get; set; }

    public int Attempts => _attempts;

    public IMailSession OpenSession() => new Session(this);

    private sealed class Session(FakeMailTransport transport) : IMailSession
    {
        public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref transport._attempts);

            // Lets another dispatcher run between two sends, as a real relay would.
            await Task.Yield();
            if (transport.AlwaysFail is { } always)
            {
                throw always;
            }

            if (transport.Failures.TryDequeue(out var failure))
            {
                throw failure;
            }

            transport.Sent.Enqueue(message);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
