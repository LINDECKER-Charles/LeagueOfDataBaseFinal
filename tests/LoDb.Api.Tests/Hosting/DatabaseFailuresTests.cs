using System.Net.Sockets;
using LoDb.Api.Hosting.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// What counts as the database failing a request, whatever EF wraps it in, and what is a
/// fault of the code that the default 500 handles.
/// </summary>
public sealed class DatabaseFailuresTests
{
    [Theory]
    [InlineData(PostgresErrorCodes.CannotConnectNow)]
    [InlineData(PostgresErrorCodes.AdminShutdown)]
    [InlineData(PostgresErrorCodes.TooManyConnections)]
    [InlineData(PostgresErrorCodes.InvalidPassword)]
    public void ServerThatRefusesTheSessionIsUnavailable(string sqlState) =>
        Assert.Equal(DatabaseFailure.Unavailable, DatabaseFailures.Of(Server(sqlState)));

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation)]
    [InlineData(PostgresErrorCodes.SerializationFailure)]
    [InlineData(PostgresErrorCodes.UndefinedTable)]
    public void ErrorOfAQueryIsNoOutage(string sqlState) =>
        Assert.Null(DatabaseFailures.Of(Server(sqlState)));

    [Fact]
    public void RefusedConnectionWrappedByEfIsUnavailable()
    {
        var refused = new NpgsqlException(
            "Failed to connect to 127.0.0.1:1",
            new SocketException((int)SocketError.ConnectionRefused));
        var wrapped = new InvalidOperationException("A transient failure.", refused);

        Assert.Equal(DatabaseFailure.Unavailable, DatabaseFailures.Of(wrapped));
    }

    [Fact]
    public void PoolTimeoutWrappedInAnUpdateIsAnExhaustedPool()
    {
        var exhausted = new NpgsqlException(
            "The connection pool has been exhausted, either raise 'Max Pool Size' (currently 1)"
            + " or 'Timeout' (currently 1 seconds) in your connection string.",
            new TimeoutException());

        Assert.Equal(
            DatabaseFailure.PoolExhausted,
            DatabaseFailures.Of(new DbUpdateException("Saving failed.", exhausted)));
    }

    [Fact]
    public void ConnectionTimeoutIsUnavailable() =>
        Assert.Equal(
            DatabaseFailure.Unavailable,
            DatabaseFailures.Of(new NpgsqlException(
                "Failed to connect to 10.0.0.1:5432",
                new TimeoutException("Timeout during connection attempt"))));

    [Fact]
    public void ClosedPoolIsNoOutage() =>
        Assert.Null(DatabaseFailures.Of(
            new NpgsqlException("The connection pool has been shut down.")));

    [Fact]
    public void ExceptionWithoutNpgsqlIsNotTheDatabase() =>
        Assert.Null(DatabaseFailures.Of(
            new InvalidOperationException("A fault.", new TimeoutException())));

    private static PostgresException Server(string sqlState) =>
        new("The server refused.", "FATAL", "FATAL", sqlState);
}
