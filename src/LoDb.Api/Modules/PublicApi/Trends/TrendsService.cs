using System.Globalization;
using LoDb.Api.Modules.PublicApi.Metering;
using LoDb.Api.Modules.PublicApi.Trends.Reading;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi.Trends;

/// <summary>
/// The rankings of <c>/v1/trends</c>: the views of each entity summed over the window, the
/// most viewed first, ties by id, 25 at most, each computed once per
/// <c>LoDb:PublicApi:TrendsLifetime</c> in this instance.
/// </summary>
/// <remarks>
/// A ranking is filed under the UTC day it ends on, so the window moves at midnight. A
/// failure of the database or of the storage is thrown and nothing is kept: the next
/// request tries again.
/// </remarks>
internal sealed partial class TrendsService(
    HybridCache cache,
    TrendsSource source,
    ITrendNames names,
    UsageCalendar calendar,
    IOptions<PublicApiOptions> options,
    ILogger<TrendsService> logger)
{
    public const int TopCount = 25;

    private const string CacheKeyPrefix = "lodb:publicapi:trends:";
    private const string DayFormat = "yyyy-MM-dd";

    private readonly HybridCacheEntryOptions _entry = new()
    {
        Expiration = options.Value.TrendsLifetime,
        LocalCacheExpiration = options.Value.TrendsLifetime,
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    public async ValueTask<TrendRanking> RankAsync(
        TrendType type,
        TrendRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(range);
        var today = calendar.Today;
        var cacheKey = string.Join(
            ':',
            CacheKeyPrefix + type.Segment,
            range.Label,
            today.ToString(DayFormat, CultureInfo.InvariantCulture));
        return await cache.GetOrCreateAsync(
            cacheKey,
            (Service: this, Type: type, Range: range, Today: today),
            static async (state, token) =>
                await state.Service.ComputeAsync(state.Type, state.Range, state.Today, token),
            _entry,
            cancellationToken: cancellationToken);
    }

    private async Task<TrendRanking> ComputeAsync(
        TrendType type,
        TrendRange range,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var days = await source.ReadEntitiesAsync(
            range.FirstDay(today),
            today,
            cancellationToken);
        var views = new Dictionary<string, long>(StringComparer.Ordinal);
        var unreadable = 0;
        foreach (var day in days)
        {
            if (!TrendDay.TryAdd(day, type.EntityType, views))
            {
                unreadable++;
            }
        }

        // Only a window of days that all fail to read tells a broken aggregate.
        if (days.Count > 0 && unreadable == days.Count)
        {
            LogUnreadable(logger, type.Segment, range.Label, unreadable);
        }

        var ranked = views
            .OrderByDescending(static entity => entity.Value)
            .ThenBy(static entity => entity.Key, StringComparer.Ordinal)
            .Take(TopCount)
            .ToList();
        var known = ranked.Count == 0
            ? new Dictionary<string, string>()
            : await names.NamesAsync(type, cancellationToken);
        return new TrendRanking([.. ranked.Select((entity, index) => new TrendEntry(
            index + 1,
            entity.Key,
            known.GetValueOrDefault(entity.Key),
            type.EditionOf(entity.Key),
            entity.Value))]);
    }

    [LoggerMessage(
        EventName = "publicapi.trends.unreadable",
        Level = LogLevel.Warning,
        Message = "Trends of {Type} over {Range}: none of the {Days} daily aggregates reads.")]
    private static partial void LogUnreadable(ILogger logger, string type, string range, int days);
}
