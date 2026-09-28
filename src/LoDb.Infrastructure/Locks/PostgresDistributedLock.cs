using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace LoDb.Infrastructure.Locks;

/// <summary>
/// <see cref="IDistributedLock"/> on a session-level PostgreSQL advisory lock
/// (<c>pg_try_advisory_lock</c>), held by a dedicated connection for as long as the lock.
/// </summary>
/// <remarks>
/// The connections are never pooled: closing one ends its session, which releases the lock
/// even if the explicit unlock never ran. A pooled connection would keep the lock until its
/// next reuse.
/// </remarks>
public sealed class PostgresDistributedLock : IDistributedLock, IDisposable, IAsyncDisposable
{
    private const string TryLock = "SELECT pg_try_advisory_lock(@key)";
    private const string Unlock = "SELECT pg_advisory_unlock(@key)";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresDistributedLock(string connectionString)
    {
        var unpooled = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
        _dataSource = NpgsqlDataSource.Create(unpooled);
    }

    /// <summary>Stable 64-bit key of a name: the first 8 bytes of its UTF-8 SHA-256.</summary>
    public static long KeyOf(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var key = KeyOf(name);
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand(TryLock, connection);
            command.Parameters.AddWithValue("key", key);
            if (await command.ExecuteScalarAsync(cancellationToken) is true)
            {
                return new Handle(connection, key);
            }
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        await connection.DisposeAsync();
        return null;
    }

    public void Dispose() => _dataSource.Dispose();

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    private sealed class Handle(NpgsqlConnection connection, long key) : IAsyncDisposable
    {
        private int _released;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1)
            {
                return;
            }

            await using (connection)
            {
                try
                {
                    await using var command = new NpgsqlCommand(Unlock, connection);
                    command.Parameters.AddWithValue("key", key);
                    await command.ExecuteScalarAsync(CancellationToken.None);
                }
                catch (NpgsqlException)
                {
                    // Closing the session below releases the lock anyway: the unlock only
                    // makes it visible sooner.
                }
            }
        }
    }
}
