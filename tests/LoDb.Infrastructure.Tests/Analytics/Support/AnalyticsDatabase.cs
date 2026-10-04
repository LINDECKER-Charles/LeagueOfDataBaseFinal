using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Infrastructure.Tests.Persistence.Analytics;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Infrastructure.Tests.Analytics.Support;

/// <summary>
/// Base of the analytics tests on a database: the zone's services on a fake clock, and
/// views written straight to <c>analytics_event</c>.
/// </summary>
public abstract class AnalyticsDatabase(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private ServiceProvider? _services;
    private FakeTimeProvider? _time;

    /// <summary>The clock, from <see cref="Start"/> on.</summary>
    protected FakeTimeProvider Time => _time ??= new FakeTimeProvider(Start);

    /// <summary>Where the clock starts: noon, UTC, on 2026-09-26.</summary>
    protected virtual DateTimeOffset Start => new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    protected DateOnly Today => DateOnly.FromDateTime(Time.GetUtcNow().UtcDateTime);

    protected T Get<T>()
        where T : notnull =>
        (_services ??= AnalyticsServices.Build(Database.ConnectionString, Time))
            .GetRequiredService<T>();

    /// <summary>One sample view at each moment, their partitions created first.</summary>
    protected async Task WriteViewsAsync(params DateTimeOffset[] moments)
    {
        var days = moments.Select(static moment => DateOnly.FromDateTime(moment.UtcDateTime))
            .ToList();
        await Get<IAnalyticsPartitions>().CreateAsync(days.Min(), days.Max(), Cancellation);
        await Get<IAnalyticsEventWriter>().WriteAsync(
            [.. moments.Select(AnalyticsSamples.View)],
            Cancellation);
    }

    protected DateTimeOffset At(int hour, int minute = 0) =>
        new(Today.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.Zero);

    protected override async ValueTask DisposeServicesAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
    }
}
