using LoDb.Infrastructure.Robots.Capture;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Infrastructure.Tests.Robots.Support;

/// <summary>
/// Base of the tests of the journal on a database: its services on a fake clock behind the
/// resolver of <see cref="JournalSamples.Dns"/>, and records written as the pump writes them.
/// </summary>
public abstract class RobotsDatabase(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private ServiceProvider? _services;
    private FakeTimeProvider? _time;

    /// <summary>The clock: noon, UTC, on 2026-10-24.</summary>
    protected FakeTimeProvider Time => _time ??= new FakeTimeProvider(
        new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

    internal FakeReverseDns Dns { get; } = JournalSamples.Dns();

    protected T Get<T>()
        where T : notnull =>
        (_services ??= RobotServices.Build(Database.AppConnectionString, Time, Dns))
            .GetRequiredService<T>();

    /// <summary>Queues the records, then drains the queue as the worker does.</summary>
    /// <returns>The hits written.</returns>
    internal async Task<int> JournalAsync(params JournalRecord[] records)
    {
        Get<IRobotJournal>().Take(records, unreadable: 0);
        return await Get<IRobotJournalPump>().FlushAsync(Cancellation);
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
    }
}
