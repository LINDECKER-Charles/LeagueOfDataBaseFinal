using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.PublicApi.Metering;

/// <summary>
/// Adds request counts to <c>api_usage</c>, one row per key and day, in a single statement
/// whatever the number of keys.
/// </summary>
internal sealed class UsageWriter(IDbContextFactory<LoDbDbContext> contexts)
{
    /// <remarks>
    /// The counts of a key deleted since its requests are dropped: its rows went with it.
    /// </remarks>
    public async Task WriteAsync(
        IReadOnlyList<KeyValuePair<UsageEvent, long>> counts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (counts.Count == 0)
        {
            return;
        }

        var keyIds = new int[counts.Count];
        var days = new DateOnly[counts.Count];
        var requests = new long[counts.Count];
        for (var index = 0; index < counts.Count; index++)
        {
            var (usage, count) = counts[index];
            keyIds[index] = usage.KeyId;
            days[index] = usage.Day;
            requests[index] = count;
        }

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO api_usage (api_key_id, day, requests)
            SELECT batch.key_id, batch.day, batch.requests
              FROM unnest({keyIds}::integer[], {days}::date[], {requests}::bigint[])
                   AS batch (key_id, day, requests)
             WHERE EXISTS (SELECT 1 FROM api_keys WHERE api_keys.id = batch.key_id)
            ON CONFLICT (api_key_id, day)
            DO UPDATE SET requests = api_usage.requests + EXCLUDED.requests
            """,
            cancellationToken);
    }
}
