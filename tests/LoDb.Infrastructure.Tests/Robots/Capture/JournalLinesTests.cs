using System.Net;
using System.Text;
using LoDb.Infrastructure.Robots.Capture;

namespace LoDb.Infrastructure.Tests.Robots.Capture;

/// <summary>
/// The body nginx posts is read line by line: a record of each object it knows, the other
/// lines counted, never thrown on.
/// </summary>
public sealed class JournalLinesTests
{
    private const string Googlebot =
        """{"t":1729771200.123,"ip":"66.249.66.1","m":"GET","""
        + """ "u":"/fr/16.1.1/items?lang=fr_FR","s":503,"""
        + """ "d":0.0426,"b":512,"a":"Googlebot/2.1","c":"MISS"}""";

    [Fact]
    public void ALineIsARequest()
    {
        var (records, unreadable) = Parse(Googlebot);

        var record = Assert.Single(records);
        Assert.Equal(0, unreadable);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_729_771_200_123), record.At);
        Assert.Equal(IPAddress.Parse("66.249.66.1"), record.Address);
        Assert.Equal(("GET", "/fr/16.1.1/items?lang=fr_FR"), (record.Method, record.Target));
        Assert.Equal(((short)503, 43, 512L), (record.Status, record.DurationMs, record.Bytes));
        Assert.Equal(("Googlebot/2.1", "MISS"), (record.Agent, record.Cache));
    }

    [Fact]
    public void AnUnknownCacheAndAnAddressAreLeftOut()
    {
        var line = Googlebot.Replace("66.249.66.1", "nope", StringComparison.Ordinal)
            .Replace("\"MISS\"", "\"\"", StringComparison.Ordinal);

        var (records, _) = Parse(line);

        var record = Assert.Single(records);
        Assert.Null(record.Address);
        Assert.Null(record.Cache);
    }

    [Fact]
    public void WhatIsNotARequestIsCountedAndSkipped()
    {
        var body = string.Join(
            '\n',
            Googlebot,
            "not json",
            """{"t":1729771200,"m":"GET","u":"no-slash","s":200}""",
            """{"t":1729771200,"m":"GET","u":"/","s":42}""",
            "",
            """{"m":"GET","u":"/","s":200}""",
            Googlebot);

        var (records, unreadable) = Parse(body);

        Assert.Equal(2, records.Count);
        Assert.Equal(4, unreadable);
    }

    private static (IReadOnlyList<JournalRecord> Records, int Unreadable) Parse(string body) =>
        JournalLines.Parse(Encoding.UTF8.GetBytes(body));
}
