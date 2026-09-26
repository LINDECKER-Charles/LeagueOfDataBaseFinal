using MailKit.Security;

namespace LoDb.Infrastructure.Outbox.Smtp;

/// <summary>
/// The mail relay (<c>LoDb:Mail</c>), checked when the host starts: Mailpit in development,
/// the provider's relay once served.
/// </summary>
/// <remarks>
/// Without a host nothing is sent: the messages wait in <c>email_outbox</c> until one is
/// set, and the worker says so once at startup. A host built without mail settings (tests,
/// OpenAPI generation) therefore neither fails nor reaches a relay.
/// </remarks>
public sealed class MailOptions
{
    public const string SectionName = "LoDb:Mail";

    /// <summary>The legacy stack's <c>MAILER_FROM</c>.</summary>
    public const string DefaultFrom = "LeagueOfDataBase <no-reply@leagueofdatabase.gg>";

    private const int DefaultPort = 587;
    private const int DefaultTimeoutSeconds = 30;

    /// <summary>Name or address of the relay; empty switches sending off.</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = DefaultPort;

    /// <summary>
    /// TLS mode: <c>Auto</c> (TLS on 465, STARTTLS when offered elsewhere), <c>None</c>,
    /// <c>SslOnConnect</c>, <c>StartTls</c> or <c>StartTlsWhenAvailable</c>.
    /// </summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;

    /// <summary>Login on the relay; none when empty.</summary>
    public string? Username { get; set; }

    /// <summary>A secret: set by the environment, never in a file of the repository.</summary>
    public string? Password { get; set; }

    /// <summary>Sender of every e-mail, an address with an optional display name.</summary>
    public string From { get; set; } = DefaultFrom;

    /// <summary>Longest wait for one exchange with the relay.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Host);
}
