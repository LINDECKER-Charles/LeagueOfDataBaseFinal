using LoDb.Infrastructure.Locks;
using LoDb.Testing;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Locks;

/// <summary>
/// The advisory lock is exclusive between connections, and released on dispose or when its
/// session dies.
/// </summary>
/// <remarks>
/// Advisory locks belong to a database: these tests lock the server's default one, which no
/// other test locks, one test at a time.
/// </remarks>
public sealed class PostgresDistributedLockTests(PostgresContainerFixture postgres)
    : IAsyncDisposable
{
    private const string Name = "job:ddragon-watch";

    private const string AdvisoryLocks = """
        FROM pg_locks
        WHERE locktype = 'advisory'
          AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
        """;

    private readonly List<PostgresDistributedLock> _locks = [];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LockIsExclusiveBetweenConnections()
    {
        var first = NewLock();
        var second = NewLock();

        await using var held = await first.TryAcquireAsync(Name, Cancellation);
        var refused = await second.TryAcquireAsync(Name, Cancellation);
        var refusedToItsOwner = await first.TryAcquireAsync(Name, Cancellation);

        Assert.NotNull(held);
        Assert.Null(refused);
        Assert.Null(refusedToItsOwner);
    }

    [Fact]
    public async Task ConcurrentAttemptsGiveTheLockOnce()
    {
        var instances = Enumerable.Range(0, 8).Select(_ => NewLock()).ToList();

        var handles = await Task.WhenAll(
            instances.Select(instance => instance.TryAcquireAsync(Name, Cancellation)));

        var held = Assert.Single(handles, static handle => handle is not null);
        await held!.DisposeAsync();
    }

    [Fact]
    public async Task DifferentNamesDoNotConflict()
    {
        var first = NewLock();
        var second = NewLock();

        await using var one = await first.TryAcquireAsync(Name, Cancellation);
        await using var other = await second.TryAcquireAsync("job:other", Cancellation);

        Assert.NotNull(one);
        Assert.NotNull(other);
    }

    [Fact]
    public async Task DisposeReleasesTheLock()
    {
        var first = NewLock();
        var second = NewLock();

        var held = await first.TryAcquireAsync(Name, Cancellation);
        await held!.DisposeAsync();
        await held.DisposeAsync();
        await using var next = await second.TryAcquireAsync(Name, Cancellation);

        Assert.NotNull(next);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) " + AdvisoryLocks));
    }

    [Fact]
    public async Task LostSessionReleasesTheLock()
    {
        var first = NewLock();
        var second = NewLock();
        var held = await first.TryAcquireAsync(Name, Cancellation);

        var terminated = await ScalarAsync<bool>(
            "SELECT bool_and(pg_terminate_backend(pid)) " + AdvisoryLocks);
        await WaitForNoAdvisoryLockAsync();
        await using var next = await second.TryAcquireAsync(Name, Cancellation);

        Assert.True(terminated);
        Assert.NotNull(next);
        // The handle of the dead session still disposes quietly.
        await held!.DisposeAsync();
    }

    [Fact]
    public void KeysAreStable()
    {
        Assert.Equal(-8296312635219940248, PostgresDistributedLock.KeyOf("db:baseline"));
        Assert.Equal(8832233758587936146, PostgresDistributedLock.KeyOf(Name));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var instance in _locks)
        {
            await instance.DisposeAsync();
        }
    }

    private PostgresDistributedLock NewLock()
    {
        var instance = new PostgresDistributedLock(postgres.ConnectionString);
        _locks.Add(instance);
        return instance;
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(Cancellation))!;
    }

    // The terminated backend releases its locks while it exits, shortly after the call.
    private async Task WaitForNoAdvisoryLockAsync()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await ScalarAsync<long>("SELECT count(*) " + AdvisoryLocks) == 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Cancellation);
        }

        Assert.Fail("The terminated session kept its advisory lock.");
    }
}
