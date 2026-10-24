using System.Net;
using LoDb.Infrastructure.Robots.Capture;

namespace LoDb.Infrastructure.Tests.Robots.Support;

/// <summary>Records as nginx reports them, for the tests of the journal.</summary>
internal static class JournalSamples
{
    /// <summary>The address of Googlebot, which reverses under googlebot.com and back.</summary>
    public const string GoogleAddress = "66.249.66.1";

    /// <summary>The name <see cref="GoogleAddress"/> reverses to.</summary>
    public const string GoogleHost = "crawl-66-249-66-1.googlebot.com";

    /// <summary>A cloud's address, reversing under example.net and back.</summary>
    public const string CloudAddress = "198.51.100.7";

    public const string CloudHost = "vm-7.compute.example.net";

    public static FakeReverseDns Dns() => new FakeReverseDns()
        .Confirmed(GoogleAddress, GoogleHost)
        .Confirmed(CloudAddress, CloudHost);

    /// <param name="at">When nginx answered.</param>
    /// <param name="agent">The user agent.</param>
    /// <param name="request">The address, the path and query, the status.</param>
    public static JournalRecord Record(
        DateTimeOffset at,
        string agent,
        (string Address, string Target, short Status) request) => new()
    {
        At = at,
        Address = IPAddress.Parse(request.Address),
        Method = "GET",
        Target = request.Target,
        Status = request.Status,
        DurationMs = 120,
        Bytes = 2048,
        Agent = agent,
        Cache = "MISS",
    };
}
