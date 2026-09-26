using LoDb.Infrastructure.Analytics;
using LoDb.Infrastructure.Analytics.Classification;
using LoDb.Infrastructure.Tests.Analytics.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Tests.Analytics.Classification;

/// <summary>
/// The visitor id is the legacy one, <c>substr(hash_hmac('sha256', ip|ua, APP_SECRET), 0,
/// 16)</c>: with the legacy secret as key, a visitor keeps its id across the switch.
/// </summary>
public sealed class VisitorHasherTests
{
    [Fact]
    public void LegacySecretGivesTheLegacyIds()
    {
        using var sample = LegacySamples.Json("visitors.json");
        var hasher = Hasher(sample.RootElement.GetProperty("secret").GetString());

        foreach (var expected in sample.RootElement.GetProperty("cases").EnumerateArray())
        {
            var ip = LegacySamples.Text(expected, "ip");
            var userAgent = LegacySamples.Text(expected, "ua");

            Assert.Equal(LegacySamples.Text(expected, "visitor"), hasher.Hash(ip, userAgent));
        }
    }

    [Fact]
    public void WithoutKeyEachProcessDrawsItsOwn()
    {
        var first = Hasher(null);
        var second = Hasher(null);

        var id = first.Hash("203.0.113.7", "curl/8.4.0");

        Assert.Equal(id, first.Hash("203.0.113.7", "curl/8.4.0"));
        Assert.NotEqual(id, second.Hash("203.0.113.7", "curl/8.4.0"));
        Assert.Matches("^[0-9a-f]{16}$", id);
    }

    private static VisitorHasher Hasher(string? key) => new(
        Options.Create(new AnalyticsOptions { VisitorKey = key }),
        NullLogger<VisitorHasher>.Instance);
}
