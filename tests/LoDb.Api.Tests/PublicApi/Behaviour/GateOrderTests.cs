using System.Net;
using LoDb.Api.Tests.PublicApi.Support;

namespace LoDb.Api.Tests.PublicApi.Behaviour;

/// <summary>
/// go-api's order: the key, then its token bucket, then its quota, the handler last; the
/// <c>X-RateLimit-*</c> headers on every answer once the key is known.
/// </summary>
[Collection(V1Group.Name)]
public sealed class GateOrderTests(V1Server server)
{
    private const string Usage = "/v1/usage";
    private const string Profile = "/v1/profiles/PublicPlayer";
    private const string RateLimited = "rate_limited";

    private static readonly string Exhausted = V1Fixtures.Key("quota_exhausted");
    private static readonly string Burst = V1Fixtures.Key("burst");

    [Fact]
    public async Task TheBucketIsCheckedBeforeTheQuota()
    {
        await using var run = await server.StartAsync();

        var answers = new List<V1Answer>();
        for (var request = 0; request < 11; request++)
        {
            answers.Add(await run.AskAsync(Profile, Exhausted));
        }

        var charged = answers.Take(10).ToList();
        Assert.All(charged, static answer => Assert.Equal("quota_exceeded", answer.ErrorCode));
        Assert.Equal([9, 8, 7, 6, 5, 4, 3, 2, 1, 0], charged.Select(static a => a.Remaining));
        var limited = answers[^1];
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.Status);
        Assert.Equal(RateLimited, limited.ErrorCode);
        Assert.Equal((10, 0), (limited.Limit, limited.Remaining));
        Assert.Equal(Unix(run, TimeSpan.FromSeconds(6)), limited.Reset);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("lodb_not-a-key", HttpStatusCode.Unauthorized)]
    [InlineData("{{key:unknown}}", HttpStatusCode.Unauthorized)]
    [InlineData("{{key:revoked}}", HttpStatusCode.Forbidden)]
    [InlineData("{{key:inactive}}", HttpStatusCode.Forbidden)]
    public async Task ARefusedKeyGetsNoRateLimit(string? key, HttpStatusCode status)
    {
        await using var run = await server.StartAsync();

        var refused = await run.AskAsync(Profile, key is null ? null : V1Fixtures.Resolve(key));

        Assert.Equal(status, refused.Status);
        Assert.Null(refused.Limit);
        Assert.Null(refused.Remaining);
        Assert.Null(refused.Reset);
    }

    [Theory]
    [InlineData("/v1/profiles/NobodyHere", HttpStatusCode.NotFound)]
    [InlineData("/v1/champions/Aatrox/builds?page=0", HttpStatusCode.BadRequest)]
    [InlineData("/v1/trends/skins", HttpStatusCode.NotFound)]
    [InlineData(Usage, HttpStatusCode.OK)]
    public async Task AKnownKeyGetsItsRateLimitWhateverTheAnswer(
        string path,
        HttpStatusCode status)
    {
        await using var run = await server.StartAsync();

        var answer = await run.AskAsync(path, Burst);

        Assert.Equal(status, answer.Status);
        Assert.Equal((10, 9), (answer.Limit, answer.Remaining));
        Assert.Equal(Unix(run, TimeSpan.FromSeconds(6)), answer.Reset);
    }

    [Fact]
    public async Task AnEmptyBucketRefillsWithTime()
    {
        await using var run = await server.StartAsync();
        for (var request = 0; request < 10; request++)
        {
            Assert.Equal(HttpStatusCode.OK, (await run.AskAsync(Usage, Burst)).Status);
        }

        var empty = await run.AskAsync(Usage, Burst);
        run.Clock.Advance(TimeSpan.FromSeconds(6));
        var oneToken = await run.AskAsync(Usage, Burst);
        run.Clock.Advance(TimeSpan.FromMinutes(1));
        var full = await run.AskAsync(Usage, Burst);

        Assert.Equal(RateLimited, empty.ErrorCode);
        Assert.Equal((HttpStatusCode.OK, 0), (oneToken.Status, oneToken.Remaining));
        Assert.Equal((HttpStatusCode.OK, 9), (full.Status, full.Remaining));
    }

    [Fact]
    public async Task UsageIsReadWithTheQuotaSpent()
    {
        await using var run = await server.StartAsync();

        var usage = await run.AskAsync(Usage, Exhausted);
        await run.FlushUsageAsync();

        Assert.Equal(HttpStatusCode.OK, usage.Status);
        Assert.Equal(500, usage.Body.GetProperty("used_this_month").GetInt64());
        Assert.Equal(0, usage.Body.GetProperty("remaining_this_month").GetInt64());
        Assert.Equal(
            "500",
            await run.Database.ScalarAsync(
                "SELECT sum(requests)::text FROM api_usage WHERE api_key_id = 10"));
    }

    private static long Unix(V1Run run, TimeSpan later) =>
        run.Clock.GetUtcNow().Add(later).ToUnixTimeSeconds();
}
