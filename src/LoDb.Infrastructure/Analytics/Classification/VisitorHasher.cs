using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Analytics.Classification;

/// <summary>
/// The pseudonymous visitor id of the legacy stack (<c>RequestEventFactory::visitorId</c>):
/// the first 16 hexadecimal digits of an HMAC-SHA256 of <c>{address}|{user agent}</c>.
/// </summary>
/// <remarks>
/// Keyed with <see cref="AnalyticsOptions.VisitorKey"/>, the legacy <c>APP_SECRET</c> at the
/// switch-over: a visitor keeps its id across the two stacks, and the id alone gives neither
/// its address nor its user agent back.
/// </remarks>
internal sealed partial class VisitorHasher
{
    private const int EphemeralKeyBytes = 32;
    private const int IdBytes = 8;
    private const string UnknownAddress = "unknown";
    private const char Separator = '|';

    private readonly byte[] _key;

    public VisitorHasher(IOptions<AnalyticsOptions> options, ILogger<VisitorHasher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configured = options.Value.VisitorKey;
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

    /// <param name="address">The client's address as text; null when unknown.</param>
    /// <param name="userAgent">The <c>User-Agent</c> header; null without one.</param>
    public string Hash(string? address, string? userAgent)
    {
        var material = Encoding.UTF8.GetBytes(
            string.Concat(address ?? UnknownAddress, Separator.ToString(), userAgent));
        return Convert.ToHexStringLower(HMACSHA256.HashData(_key, material), 0, IdBytes);
    }

    [LoggerMessage(
        EventName = "analytics.visitor_key.ephemeral",
        Level = LogLevel.Warning,
        Message = "No visitor key is configured (LoDb:Analytics:VisitorKey): visitor ids change"
            + " with every process, so returning visitors are undercounted.")]
    private static partial void LogEphemeralKey(ILogger logger);
}
