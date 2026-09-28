using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MimeKit;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>
/// A mail relay on the loopback that accepts every message and keeps it: the outbox sends
/// to it through its real SMTP client, as it would to Mailpit.
/// </summary>
/// <remarks>
/// It speaks the least of SMTP a client needs, without extensions: MailKit then sends 7-bit
/// messages, one command at a time.
/// </remarks>
public sealed class SmtpSink : IAsyncDisposable
{
    public const string Host = "127.0.0.1";

    private const string EndOfData = ".";

    // Latin-1 maps every byte to one character and back: the message is read as sent.
    private static readonly Encoding Wire = Encoding.Latin1;

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentQueue<MimeMessage> _messages = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accepting;

    public SmtpSink()
    {
        _listener.Start();
        _accepting = AcceptAsync(_stop.Token);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>The messages received, oldest first.</summary>
    public IReadOnlyList<MimeMessage> Messages => [.. _messages];

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        await _accepting;
        _stop.Dispose();
    }

    private async Task AcceptAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = ServeAsync(client, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or SocketException or ObjectDisposedException)
        {
            // Stopped by DisposeAsync.
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var connection = client;
        try
        {
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Wire);
            await using var writer = new StreamWriter(stream, Wire) { AutoFlush = true };
            await ReplyAsync(writer, "220 sink ESMTP");
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                var verb = line.Split(' ', 2)[0].ToUpperInvariant();
                if (verb == "QUIT")
                {
                    await ReplyAsync(writer, "221 bye");
                    return;
                }

                if (verb == "DATA")
                {
                    await ReplyAsync(writer, "354 end with a lone dot");
                    _messages.Enqueue(await ReadMessageAsync(reader, cancellationToken));
                }

                await ReplyAsync(writer, verb == "EHLO" ? "250 sink" : "250 ok");
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or IOException or ObjectDisposedException)
        {
            // The client or the sink went away.
        }
    }

    private static async Task<MimeMessage> ReadMessageAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line && line != EndOfData)
        {
            // Dot-stuffing: a leading dot of the content is sent doubled.
            data.Append(line.StartsWith(EndOfData, StringComparison.Ordinal) ? line[1..] : line)
                .Append("\r\n");
        }

        using var content = new MemoryStream(Wire.GetBytes(data.ToString()));
        return await MimeMessage.LoadAsync(content, cancellationToken);
    }

    private static Task ReplyAsync(StreamWriter writer, string reply) =>
        writer.WriteAsync(reply + "\r\n");
}
