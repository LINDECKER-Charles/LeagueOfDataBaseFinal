using System.Globalization;
using System.Net;
using LoDb.Api.Tests.PublicApi.Support;

namespace LoDb.Api.Tests.PublicApi.Behaviour;

/// <summary>
/// The billed requests reach <c>api_usage</c> in groups, on the day they were served, and
/// none is lost to a failed write or to a shutdown; <c>/v1/usage</c> is never counted.
/// </summary>
[Collection(V1Group.Name)]
public sealed class MeteringTests(V1Server server)
{
    private const string Profile = "/v1/profiles/PublicPlayer";
    private const string Usage = "/v1/usage";
    private const int BillingId = 12;
    private const int TargetId = 13;

    private static readonly string Billing = V1Fixtures.Key("billing");

    [Fact]
    public async Task BilledRequestsAreCountedUsageIsNot()
    {
        await using var run = await server.StartAsync();

        await AskAsync(run, Profile, HttpStatusCode.OK);
        await AskAsync(run, "/v1/profiles/NobodyHere", HttpStatusCode.NotFound);
        await AskAsync(run, "/v1/champions/Aatrox/builds?page=0", HttpStatusCode.BadRequest);
        await AskAsync(run, Usage, HttpStatusCode.OK);
        var before = await AskAsync(run, Usage, HttpStatusCode.OK);
        await run.FlushUsageAsync();
        var after = await AskAsync(run, Usage, HttpStatusCode.OK);

        Assert.Equal(0, before.Body.GetProperty("used_this_month").GetInt64());
        Assert.Equal(3, after.Body.GetProperty("used_this_month").GetInt64());
        Assert.Equal([(run.SeedDay, "3")], await RowsAsync(run, BillingId));
    }

    [Fact]
    public async Task ARequestCountsOnTheDayItWasServed()
    {
        await using var run = await server.StartAsync();
        run.Clock.Advance(TimeSpan.FromHours(12) - TimeSpan.FromSeconds(1));

        await AskAsync(run, Profile, HttpStatusCode.OK);
        run.Clock.Advance(TimeSpan.FromSeconds(2));
        await AskAsync(run, Profile, HttpStatusCode.OK);
        await run.FlushUsageAsync();

        Assert.Equal(
            [(run.SeedDay, "1"), (run.SeedDay.AddDays(1), "1")],
            await RowsAsync(run, BillingId));
    }

    [Fact]
    public async Task AFailedWriteKeepsItsCountsForTheNextOne()
    {
        await using var run = await server.StartAsync();
        await AskAsync(run, Profile, HttpStatusCode.OK);
        await AskAsync(run, Profile, HttpStatusCode.OK);

        await run.Database.StopAsync();
        await run.FlushUsageAsync();
        await run.Database.StartAsync();

        // A connection the outage broke may fail one more write: none loses a request.
        for (var flush = 0; flush < 5 && (await RowsAsync(run, BillingId)).Count == 0; flush++)
        {
            await run.FlushUsageAsync();
        }

        Assert.Equal([(run.SeedDay, "2")], await RowsAsync(run, BillingId));
    }

    [Fact]
    public async Task TheLastRequestsAreWrittenAtShutdown()
    {
        await using var run = await server.StartAsync();
        await AskAsync(run, Profile, HttpStatusCode.OK);
        await AskAsync(run, Profile, HttpStatusCode.OK);
        await AskAsync(run, Profile, HttpStatusCode.OK);

        await run.StopApiAsync();

        Assert.Equal([(run.SeedDay, "3")], await RowsAsync(run, BillingId));
    }

    [Fact]
    public async Task TheRequestsOfADeletedKeyAreDropped()
    {
        await using var run = await server.StartAsync();
        await AskAsync(run, Profile, HttpStatusCode.OK);
        var target = await run.AskAsync(Profile, V1Fixtures.Key("revocation_target"));
        Assert.Equal(HttpStatusCode.OK, target.Status);

        await run.Database.ExecuteAsync($"DELETE FROM api_keys WHERE id = {TargetId}");
        await run.FlushUsageAsync();

        Assert.Equal([(run.SeedDay, "1")], await RowsAsync(run, BillingId));
        Assert.Empty(await RowsAsync(run, TargetId));
    }

    private static async Task<V1Answer> AskAsync(V1Run run, string path, HttpStatusCode status)
    {
        var answer = await run.AskAsync(path, Billing);
        Assert.Equal(status, answer.Status);
        return answer;
    }

    // The rows of api_usage of the key, by day.
    private static async Task<List<(DateOnly Day, string Requests)>> RowsAsync(
        V1Run run,
        int keyId)
    {
        var rows = await run.Database.ScalarAsync(
            $"""
            SELECT string_agg(day::text || '=' || requests, ',' ORDER BY day)
              FROM api_usage WHERE api_key_id = {keyId}
            """);
        return rows is null ? [] : [.. rows.Split(',').Select(Row)];
    }

    private static (DateOnly Day, string Requests) Row(string row)
    {
        var parts = row.Split('=');
        var day = DateOnly.ParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (day, parts[1]);
    }
}
