using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LoDb.Api.Tests.Billing.Support;

/// <summary>
/// Signs a payload as Stripe does: <c>t=</c> the Unix time, <c>v1=</c> the HMAC-SHA256 of
/// <c>{t}.{payload}</c> under the webhook secret, in lowercase hexadecimal.
/// </summary>
public static class WebhookSigner
{
    public const string Header = "Stripe-Signature";

    public static string Sign(string payload, DateTimeOffset at, string secret)
    {
        var timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var mac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(mac)}";
    }
}
