using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LoDb.Infrastructure.Analytics.Retention;

/// <summary>
/// Partitions ahead, retention behind, on the clock of <see cref="TimeProvider"/>. The daily
/// aggregates hold neither address nor user agent and are left alone.
/// </summary>
internal sealed class AnalyticsRetention(
    IAnalyticsPartitions partitions,
    NpgsqlDataSource dataSource,
    IOptions<AnalyticsOptions> options,
    TimeProvider timeProvider) : IAnalyticsRetention
{
    private const string EraseSql = """
        UPDATE analytics_event SET ip = NULL, user_agent = NULL
        WHERE occurred_at < @cutoff AND (ip IS NOT NULL OR user_agent IS NOT NULL)
        """;

    public async Task<RetentionSummary> ApplyAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var created = await partitions.CreateAsync(
            today.AddDays(-1),
            today.AddDays(settings.PartitionsAhead),
            cancellationToken);
        var dropped = await partitions.DropBeforeAsync(
            today.AddMonths(-settings.EventRetentionMonths),
            cancellationToken);
        await using var erase = dataSource.CreateCommand(EraseSql);
        erase.Parameters.AddWithValue("cutoff", now - settings.ClientDataRetention);
        var erased = await erase.ExecuteNonQueryAsync(cancellationToken);
        return new RetentionSummary
        {
            PartitionsCreated = created,
            PartitionsDropped = dropped,
            ClientDataErased = erased,
        };
    }
}
