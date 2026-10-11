using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LoDb.Infrastructure.Analytics.Retention;

/// <summary>
/// Partitions ahead, retention behind, on the clock of <see cref="TimeProvider"/>. The daily
/// aggregates hold neither address nor user agent and are left alone.
/// </summary>
/// <remarks>
/// The capture no longer stores addresses nor user agents: the erasure only clears the views
/// written before, as they come of age. It reads the partitions next to the cutoff alone,
/// from <see cref="AnalyticsOptions.ClientDataEraseDays"/> days before the cutoff's day, so
/// that two runs overlap even when a restart or a failure puts days between them: an
/// unbounded one would scan every older partition each day, to rewrite none of them.
/// </remarks>
internal sealed class AnalyticsRetention(
    IAnalyticsPartitions partitions,
    NpgsqlDataSource dataSource,
    IOptions<AnalyticsOptions> options,
    TimeProvider timeProvider) : IAnalyticsRetention
{
    private const string EraseSql = """
        UPDATE analytics_event SET ip = NULL, user_agent = NULL
        WHERE occurred_at >= @from AND occurred_at < @cutoff
            AND (ip IS NOT NULL OR user_agent IS NOT NULL)
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
        return new RetentionSummary
        {
            PartitionsCreated = created,
            PartitionsDropped = dropped,
            ClientDataErased = await EraseClientDataAsync(settings, now, cancellationToken),
        };
    }

    private async Task<int> EraseClientDataAsync(
        AnalyticsOptions settings,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cutoff = now - settings.ClientDataRetention;
        var firstDay = DateOnly.FromDateTime(cutoff.UtcDateTime)
            .AddDays(-settings.ClientDataEraseDays);
        await using var erase = dataSource.CreateCommand(EraseSql);
        erase.Parameters.AddWithValue("from", AnalyticsPartitionNames.StartOf(firstDay));
        erase.Parameters.AddWithValue("cutoff", cutoff);
        return await erase.ExecuteNonQueryAsync(cancellationToken);
    }
}
