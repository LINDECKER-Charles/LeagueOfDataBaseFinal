using LoDb.Testing;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// Base of the tests that need a database at the latest migration: one per test, dropped
/// after it.
/// </summary>
public abstract class MigratedDatabase(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private TestDatabase? _database;

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    protected TestDatabase Database =>
        _database ?? throw new InvalidOperationException("The database is not created yet.");

    public async ValueTask InitializeAsync()
    {
        _database = await postgres.CreateDatabaseAsync(Cancellation);
        await _database.MigrateAsync(Cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeServicesAsync();
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Releases what the test built on the database, before the database goes.</summary>
    protected virtual ValueTask DisposeServicesAsync() => ValueTask.CompletedTask;
}
