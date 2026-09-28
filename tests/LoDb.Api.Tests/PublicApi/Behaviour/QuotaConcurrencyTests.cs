using System.Net;
using LoDb.Api.Tests.PublicApi.Support;

namespace LoDb.Api.Tests.PublicApi.Behaviour;

/// <summary>
/// Requests of one key at once pay exactly what is left: the quota of the plan first, then
/// each credit once, the others refused with <c>429 quota_exceeded</c>.
/// </summary>
[Collection(V1Group.Name)]
public sealed class QuotaConcurrencyTests(V1Server server)
{
    private const string Profile = "/v1/profiles/PublicPlayer";
    private const int Requests = 60;

    [Fact]
    public async Task TheLastRequestOfThePlanThenEachCreditPayOnce()
    {
        await using var run = await server.StartAsync();
        // quota_edge: 499 requests of 500 this month; credits and bucket raised for the race.
        await run.Database.ExecuteAsync(
            "UPDATE api_keys SET credits_balance = 25, rate_limit_per_min = 1000 WHERE id = 9");

        var answers = await RaceAsync(run, V1Fixtures.Key("quota_edge"));
        await run.FlushUsageAsync();

        Assert.Equal(1 + 25, answers.Count(static answer => answer.Status == HttpStatusCode.OK));
        Assert.All(
            answers.Where(static answer => answer.Status != HttpStatusCode.OK),
            static answer => Assert.Equal("quota_exceeded", answer.ErrorCode));
        Assert.Equal(
            "0",
            await run.Database.ScalarAsync(
                "SELECT credits_balance::text FROM api_keys WHERE id = 9"));
        Assert.Equal("525", await UsedAsync(run, 9));
    }

    [Fact]
    public async Task ThePlanAloneIsNeverOverspent()
    {
        await using var run = await server.StartAsync();
        // monthly: 137 requests this month; 20 left, no credits.
        await run.Database.ExecuteAsync(
            "UPDATE api_keys SET monthly_quota = 157, rate_limit_per_min = 1000 WHERE id = 3");

        var answers = await RaceAsync(run, V1Fixtures.Key("monthly"));
        await run.FlushUsageAsync();

        Assert.Equal(20, answers.Count(static answer => answer.Status == HttpStatusCode.OK));
        Assert.Equal(
            Requests - 20,
            answers.Count(static answer => answer.ErrorCode == "quota_exceeded"));
        Assert.Equal("157", await UsedAsync(run, 3));
    }

    private static async Task<V1Answer[]> RaceAsync(V1Run run, string key)
    {
        using var start = new SemaphoreSlim(0);
        var requests = Enumerable.Range(0, Requests)
            .Select(async _ =>
            {
                await start.WaitAsync(V1Server.Token);
                return await run.AskAsync(Profile, key);
            })
            .ToList();
        start.Release(Requests);
        return await Task.WhenAll(requests);
    }

    // The billed requests api_usage holds for the month of the seed day.
    private static Task<string?> UsedAsync(V1Run run, int keyId) =>
        run.Database.ScalarAsync(
            $"""
            SELECT sum(requests)::text FROM api_usage
             WHERE api_key_id = {keyId} AND day >= date_trunc('month', CURRENT_DATE)
            """);
}
