namespace LoDb.Infrastructure.Analytics.Aggregation;

/// <summary>
/// The bounds of a day folded from its views, the two parts of it a client controls: the
/// visitor hashes (one per address and user agent) and the referring hosts (any Referer).
/// </summary>
internal sealed record DailyLimits
{
    /// <summary>No bound: an aggregate read back as it was stored, or imported.</summary>
    public static DailyLimits None { get; } = new()
    {
        MaxVisitors = int.MaxValue,
        MaxRefererHosts = int.MaxValue,
    };

    /// <summary>Distinct visitor hashes kept; the next ones are left out and flagged.</summary>
    public required int MaxVisitors { get; init; }

    /// <summary>
    /// Referring hosts counted by name; the others are counted under
    /// <see cref="DailyBuckets.OtherRefererHosts"/>.
    /// </summary>
    public required int MaxRefererHosts { get; init; }

    public static DailyLimits Of(AnalyticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new DailyLimits
        {
            MaxVisitors = options.MaxVisitorsPerDay,
            MaxRefererHosts = options.MaxRefererHostsPerDay,
        };
    }
}
