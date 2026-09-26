using System.Net.Sockets;
using LoDb.Infrastructure.Outbox.Rendering;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>
/// A failed delivery as a short code, the only trace of it in the table and the logs.
/// </summary>
/// <remarks>
/// The text of a mail exception is the relay's: a refusal echoes the recipient's address
/// (<c>550 5.1.1 &lt;player@example.com&gt;</c>) and a failed login the account's name. The
/// exception is therefore never logged, only this code, as the legacy stack did
/// (<c>DeliveryFailure.php</c>); the relay's own logs hold the rest.
/// </remarks>
internal static class DeliveryFailure
{
    private const int MaxLength = 64;

    public static string Code(Exception exception) => exception switch
    {
        SmtpCommandException command => $"smtp.{(int)command.StatusCode}",
        SmtpProtocolException => "smtp.protocol",
        AuthenticationException => "smtp.auth",
        SslHandshakeException => "smtp.tls",
        SocketException socket => $"socket.{socket.SocketErrorCode}",
        TimeoutException or OperationCanceledException => "timeout",
        IOException => "io",
        EmailModelException => "model.invalid",
        ParseException => "recipient.invalid",
        UnknownStoredValueException => "stored.unknown",
        _ => Clip(exception.GetType().Name),
    };

    /// <summary>
    /// True when another attempt would fail the same way: the stored message itself is at
    /// fault, not the relay.
    /// </summary>
    public static bool IsPermanent(Exception exception) =>
        exception is EmailModelException or ParseException or UnknownStoredValueException;

    private static string Clip(string code) => code.Length > MaxLength ? code[..MaxLength] : code;
}
