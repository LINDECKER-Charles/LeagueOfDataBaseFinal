using System.Diagnostics;
using System.Net;
using LoDb.Api.Tests.PublicApi.Support;

namespace LoDb.Api.Tests.PublicApi.Behaviour;

/// <summary>
/// A change of a key applies to the next request once its author calls
/// <c>IApiKeyCache.Invalidate</c>, where go-api waited for its cache to expire; a change
/// nobody reports shows within <c>KeyCacheLifetime</c>. Revocation, plan changes and top-ups
/// are replayed by the recorded contract (<c>09-key-cache</c>).
/// </summary>
[Collection(V1Group.Name)]
public sealed class ImmediateRevocationTests(V1Server server)
{
    private const string Usage = "/v1/usage";
    private const int TargetId = 13;

    private static readonly string Target = V1Fixtures.Key("revocation_target");
    private static readonly string Late = V1Fixtures.Key("late_key");

    [Fact]
    public async Task ADeletedKeyIsUnknownAtOnce()
    {
        await using var run = await server.StartAsync();
        Assert.Equal(HttpStatusCode.OK, (await run.AskAsync(Usage, Target)).Status);
        var key = await run.KeyAsync(TargetId);

        await run.Database.ExecuteAsync($"DELETE FROM api_keys WHERE id = {TargetId}");
        run.Invalidate(key);
        var deleted = await run.AskAsync(Usage, Target);

        Assert.Equal(HttpStatusCode.Unauthorized, deleted.Status);
        Assert.Equal("unauthorized", deleted.ErrorCode);
    }

    [Fact]
    public async Task ARegeneratedKeyReplacesTheOldOneAtOnce()
    {
        await using var run = await server.StartAsync();
        Assert.Equal(HttpStatusCode.OK, (await run.AskAsync(Usage, Target)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await run.AskAsync(Usage, Late)).Status);

        await run.ChangeKeysAsync(
            $"""
            UPDATE api_keys
               SET key_hash = encode(sha256(convert_to('{Late}', 'UTF8')), 'hex')
             WHERE id = {TargetId}
            """);

        Assert.Equal(HttpStatusCode.Unauthorized, (await run.AskAsync(Usage, Target)).Status);
        Assert.Equal(HttpStatusCode.OK, (await run.AskAsync(Usage, Late)).Status);
    }

    [Fact]
    public async Task AChangeNobodyReportsShowsWithinTheLifetime()
    {
        var lifetime = TimeSpan.FromMilliseconds(200);
        await using var run = await server.StartAsync(new Dictionary<string, string?>
        {
            ["LoDb:PublicApi:KeyCacheLifetime"] = "00:00:00.200",
        });
        Assert.Equal(HttpStatusCode.OK, (await run.AskAsync(Usage, Target)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await run.AskAsync(Usage, Late)).Status);

        await run.Database.ExecuteAsync(
            $"""
            UPDATE api_keys SET is_active = false WHERE id = {TargetId};
            INSERT INTO api_keys (id, user_id, name, key_hash, key_prefix, plan, monthly_quota,
                                  credits_balance, rate_limit_per_min, is_active, created_at)
            VALUES (16, 25, 'late_key', encode(sha256(convert_to('{Late}', 'UTF8')), 'hex'),
                    'lodb_a644581', 'free', 500, 0, 10, true, now());
            """);

        // The known keys expire on the wall clock, the unknown ones on the API's.
        var waited = Stopwatch.StartNew();
        (HttpStatusCode Target, HttpStatusCode Late) seen;
        do
        {
            run.Clock.Advance(lifetime);
            await Task.Delay(lifetime, V1Server.Token);
            seen = ((await run.AskAsync(Usage, Target)).Status,
                (await run.AskAsync(Usage, Late)).Status);
        }
        while (seen != (HttpStatusCode.Forbidden, HttpStatusCode.OK)
            && waited.Elapsed < TimeSpan.FromSeconds(10));

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.OK), seen);
    }
}
