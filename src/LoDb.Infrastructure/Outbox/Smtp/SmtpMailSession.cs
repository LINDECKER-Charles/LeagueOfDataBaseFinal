using MailKit.Net.Smtp;
using MimeKit;

namespace LoDb.Infrastructure.Outbox.Smtp;

/// <summary>
/// One MailKit client for a batch: connected at the first message, reconnected when a
/// failure has closed it, quit when the batch ends.
/// </summary>
/// <remarks>
/// A refused command (a rejected recipient) leaves the connection open; a broken one (I/O,
/// protocol) closes it, and the next message opens another.
/// </remarks>
internal sealed class SmtpMailSession(MailOptions options) : IMailSession
{
    private readonly SmtpClient _client = new()
    {
        Timeout = (int)options.Timeout.TotalMilliseconds,
    };

    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
        {
            await ConnectAsync(cancellationToken);
        }

        await _client.SendAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync(quit: true, CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is IOException or SmtpProtocolException)
        {
            // Every message of the batch is already recorded: a failed QUIT changes nothing.
        }
        finally
        {
            _client.Dispose();
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var host = options.Host
            ?? throw new InvalidOperationException("No mail relay is configured.");
        await _client.ConnectAsync(host, options.Port, options.Security, cancellationToken);
        if (!string.IsNullOrEmpty(options.Username))
        {
            await _client.AuthenticateAsync(
                options.Username,
                options.Password ?? string.Empty,
                cancellationToken);
        }
    }
}
