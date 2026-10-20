using System.Net;
using LoDb.Api.Tests.Admin.Scaleway.Support;
using Microsoft.Extensions.Logging;

namespace LoDb.Api.Tests.Admin.Scaleway;

/// <summary>
/// A reading that fails: an error status, no answer in time, an answer that is not the bill,
/// or one without its list.
/// The panels then show the last good bill, flagged stale, or nothing; the next attempt waits
/// five minutes; and no log line ever holds the key.
/// </summary>
public sealed class ScalewayBillFailureTests : IDisposable
{
    private const string Unavailable = "admin.scaleway_bill.unavailable";

    private readonly ScalewayHarness _harness = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> Failures =>
        ["403", "500", "timeout", "unreachable", "html", "no list"];

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task FailureWithoutAnEarlierBillLeavesItUnavailable(string failure)
    {
        _harness.Scaleway.Answer = _ => Fail(failure);

        var bill = await _harness.Reader.ReadAsync(Token);

        Assert.True(bill.Configured);
        Assert.True(bill.Stale);
        Assert.Null(bill.FetchedAt);
        Assert.Null(bill.Backups);
        Assert.Null(bill.Pages);
        var log = Assert.Single(_harness.Logs.GetSnapshot());
        Assert.Equal(Unavailable, log.Id.Name);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.NotNull(log.Exception);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task FailureKeepsTheLastBillStaleAndWaitsBeforeTheNextAttempt(string failure)
    {
        var good = await _harness.Reader.ReadAsync(Token);
        _harness.Clock.Advance(TimeSpan.FromHours(1));
        _harness.Scaleway.Answer = _ => Fail(failure);

        var stale = await _harness.Reader.ReadAsync(Token);
        _harness.Clock.Advance(TimeSpan.FromMinutes(4));
        var held = await _harness.Reader.ReadAsync(Token);

        Assert.True(stale.Stale);
        Assert.Equal(good.FetchedAt, stale.FetchedAt);
        Assert.Equal(good.Pages, stale.Pages);
        Assert.True(held.Stale);
        Assert.Equal(12, _harness.Scaleway.Calls.Count);
        Assert.Single(_harness.Logs.GetSnapshot(), static log => log.Id.Name == Unavailable);

        _harness.Clock.Advance(TimeSpan.FromMinutes(1));
        _harness.Scaleway.Answer = ScalewayAnswers.Default;
        var fresh = await _harness.Reader.ReadAsync(Token);

        Assert.False(fresh.Stale);
        Assert.Equal(_harness.Clock.GetUtcNow(), fresh.FetchedAt);
        Assert.Equal(18, _harness.Scaleway.Calls.Count);
    }

    [Fact]
    public async Task NoLogLineHoldsTheKey()
    {
        await _harness.Reader.ReadAsync(Token);
        _harness.Clock.Advance(TimeSpan.FromHours(1));
        _harness.Scaleway.Answer = static _ => Fail("403");
        await _harness.Reader.ReadAsync(Token);

        var logs = _harness.Logs.GetSnapshot();

        Assert.Contains(logs, static log => log.Id.Name == Unavailable);
        Assert.All(logs, static log =>
        {
            Assert.DoesNotContain(ScalewayHarness.Key, log.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(
                ScalewayHarness.Key,
                log.Exception?.ToString() ?? string.Empty,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                log.StructuredState ?? [],
                static pair =>
                    pair.Value?.Contains(ScalewayHarness.Key, StringComparison.Ordinal) == true);
        });
    }

    public void Dispose() => _harness.Dispose();

    private static HttpResponseMessage Fail(string failure) => failure switch
    {
        "403" => ScalewayAnswers.Json(
            """{"type":"denied_authentication"}""",
            HttpStatusCode.Forbidden),
        "500" => ScalewayAnswers.Json(
            """{"message":"internal error"}""",
            HttpStatusCode.InternalServerError),
        "timeout" => throw new TaskCanceledException("timeout", new TimeoutException()),
        "unreachable" => throw new HttpRequestException("Connection refused"),
        // A 200 whose list is null: the answer reads, but holds no bill.
        "no list" => ScalewayAnswers.Json("""{"consumptions":null,"total_count":0}"""),
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>maintenance</html>"),
        },
    };
}
