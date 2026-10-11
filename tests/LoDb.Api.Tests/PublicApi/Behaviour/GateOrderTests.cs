using System.Net;
using LoDb.Api.Tests.PublicApi.Support;

namespace LoDb.Api.Tests.PublicApi.Behaviour;

/// <summary>
/// go-api's order: the key, then its token bucket, then its quota, the handler last. The
/// <c>X-RateLimit-*</c> headers of refused and known keys are replayed by the recorded
/// contract (<c>01-auth</c>, <c>06-limits</c>, <c>07-billing</c>).
/// </summary>
[Collection(V1Group.Name)]
public sealed class GateOrderTests(V1Server server)
{
    private const string Profile = "/v1/profiles/PublicPlayer";
    private const string RateLimited = "rate_limited";

    private static readonly string Exhausted = V1Fixtures.Key("quota_exhausted");

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

    private static long Unix(V1Run run, TimeSpan later) =>
        run.Clock.GetUtcNow().Add(later).ToUnixTimeSeconds();
}
