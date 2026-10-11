using System.Security.Cryptography;
using System.Text;
using LoDb.Api.Modules.Accounts.Registration;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Http;

/// <summary>
/// What the journal keeps of an identifier typed on a failed sign-in: its kind, and a keyed
/// digest that matches the attempts on one identifier without holding its text.
/// </summary>
/// <remarks>
/// <para>
/// The text would keep, for the months of the journal and in every dump, the e-mails of
/// strangers tried from lists, and now and then a password typed in the wrong field. Input
/// that has the shape of neither an e-mail nor a username is kept as nothing at all.
/// </para>
/// <para>
/// An HMAC rather than a plain hash: whoever reads the table without the key cannot hash a
/// list of addresses to find which ones were tried.
/// </para>
/// </remarks>
internal sealed partial class IdentifierDigest
{
    /// <summary>Kind of an identifier with an <c>@</c>, which no username has.</summary>
    public const string EmailKind = "email";

    public const string UsernameKind = "username";

    private const int EphemeralKeyBytes = 32;

    private readonly byte[] _key;

    public IdentifierDigest(
        IOptions<AuditIdentifierOptions> options,
        ILogger<IdentifierDigest> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configured = options.Value.IdentifierKey;
        if (string.IsNullOrEmpty(configured))
        {
            _key = RandomNumberGenerator.GetBytes(EphemeralKeyBytes);
            LogEphemeralKey(logger);
        }
        else
        {
            _key = Encoding.UTF8.GetBytes(configured);
        }
    }

    /// <summary>
    /// The kind of <paramref name="identifier"/>, <see cref="EmailKind"/> or
    /// <see cref="UsernameKind"/>; null when it has the shape of neither.
    /// </summary>
    public static string? KindOf(string? identifier)
    {
        var trimmed = identifier?.Trim() ?? string.Empty;
        if (RegistrationRules.IsEmail(RegistrationRules.Email(trimmed)))
        {
            return EmailKind;
        }

        return RegistrationRules.IsUsername(trimmed) ? UsernameKind : null;
    }

    /// <summary>
    /// The digest of <paramref name="identifier"/>, whatever its case and its surrounding
    /// spaces, as an account is looked up.
    /// </summary>
    public string Of(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var normalized = identifier.Trim().ToUpperInvariant();
        return Convert.ToHexStringLower(
            HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(normalized)));
    }

    [LoggerMessage(
        EventName = "accounts.identifier_key.ephemeral",
        Level = LogLevel.Warning,
        Message = "No identifier key is configured (LoDb:Audit:IdentifierKey): the journal"
            + " matches the attempts on one identifier within this process only.")]
    private static partial void LogEphemeralKey(ILogger logger);
}
